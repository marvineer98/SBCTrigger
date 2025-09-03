using DuetAPIClient;

namespace SBCTrigger
{
    public enum RunCondition
    {
        Always = 0,         // R0 – Trigger at any time (default)
        WhilePrinting = 1,  // R1 – Only trigger while printing from SD card
        NotPrinting = 2,    // R2 – Only trigger when not printing
        Disabled = -1       // R-1 – Temporarily disables the trigger
    }

    public partial class SBCTrigger
    {
        // Connection used for commanding codes and evaluate expressions
        private CommandConnection Connection { get; } = new();

        private string expresssion;

        private string action;

        private RunCondition runCondition;

        private bool triggered;
        private Task? task;

        public SBCTrigger(string expresssionString, string actionString, bool initialState = false, RunCondition runCondition = RunCondition.Always)
        {
            Connection.Connect();
            expresssion = expresssionString;
            action = actionString;
            triggered = initialState;
            // Set the run condition and start the task if needed
            SetRunCondition(runCondition);
        }

        public void UpdateParams(string? stateExpression, string? actionString, RunCondition? newRunCondition)
        {
            if (!string.IsNullOrWhiteSpace(stateExpression))
                expresssion = stateExpression;
            if (!string.IsNullOrWhiteSpace(actionString))
                action = actionString;
            if (newRunCondition.HasValue && newRunCondition.Value != runCondition)
                SetRunCondition(newRunCondition.Value);
        }

        public void SetRunCondition(RunCondition newCondition)
        {
            if (!Enum.IsDefined(typeof(RunCondition), newCondition))
            {
                throw new ArgumentOutOfRangeException(nameof(newCondition), "Invalid RunCondition value: " + newCondition);
            }
            runCondition = newCondition;
            if (!runCondition.Equals(RunCondition.Disabled) && (task == null || task.IsCompleted))
            {
                // Start the trigger if it was not running
                task = Loop();
            }
        }

        public override string ToString()
        {
            return $"Expression: '{expresssion}', Action: '{action}', RunCondition: {runCondition}, Triggered: {triggered}";
        }

        private async Task Loop()
        {
            while (!runCondition.Equals(RunCondition.Disabled))
            {
                try
                {
                    // wait 0.25 seconds before re-evaluating
                    // this is the update frequency of the object model
                    await Task.Delay(250);

                    // skip evaluation if run condition is not met
                    if (runCondition != RunCondition.Always)
                    {
                        // check if we are printing from SD card
                        // job.build exists and state.status is not "simulating"
                        var jobFileexists = (await Connection.EvaluateExpression("exists(job.build)")).ToString() == "True";
                        var status = (await Connection.EvaluateExpression("state.status")).ToString();
                        var isPrinting = jobFileexists && status != "simulating";
                        // continue when runCondition is WhilePrinting and we are not printing
                        // or NotPrinting and we are printing
                        if (runCondition == RunCondition.WhilePrinting != isPrinting)
                            continue;
                    }

                    // evaluate the expression
                    var result = string.Empty;
                    try
                    {
                        result = Connection.EvaluateExpression(expresssion).Result.ToString();

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"SBCTrigger: Failed to evaluate expression '{expresssion}', deactivating the corresponding trigger for now: {e.Message}");
                        SetRunCondition(RunCondition.Disabled);
                        break;
                    }

                    // compare the result with the trigger value
                    bool conditionMet = result == "True";

                    // check if the condition is met and if we have not triggered yet
                    if (!triggered && conditionMet)
                    {
                        // we need to trigger now
                        try
                        {
                            await Connection.PerformSimpleCode(action);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"SBCTrigger: Failed to trigger the action '{action}', deactivating the corresponding trigger for now: {e.Message}");
                            SetRunCondition(RunCondition.Disabled);
                            break;
                        }
                        // set the trigger flag
                        triggered = true;
                        continue;
                    }
                    // check if we have triggered and the condition is no longer met
                    else if (triggered && !conditionMet)
                    {
                        // Condition no longer met, reset trigger
                        triggered = false;
                        continue;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"SBCTrigger failed, deactivating the corresponding trigger for now: {e.Message}");
                    SetRunCondition(RunCondition.Disabled);
                    break;
                }
            }
        }
    }
}