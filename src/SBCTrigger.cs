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

        private readonly string stateExpression;
        private readonly string stateOperation;
        private readonly object stateTriggerValue;

        private readonly string action;

        private RunCondition runCondition;

        private bool triggered = false;
        private Task? task;

        public SBCTrigger(string expresssionString, string code, RunCondition runCondition = RunCondition.Always)
        {
            Connection.Connect();
            var match = ExpressionRegex().Match(expresssionString);
            if (!match.Success)
            {
                throw new ArgumentException("Invalid expression format. Needs to be in this format: <node><condition><value>", nameof(expresssionString));
            }
            stateExpression = match.Groups[1].Value.Trim();
            stateOperation = match.Groups[2].Value.Trim();
            stateTriggerValue = match.Groups[3].Value.Trim();
            action = code;
            // Set the run condition and start the task if needed
            SetRunCondition(runCondition);
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
            return $"Expression: '{stateExpression} {stateOperation} {stateTriggerValue}', Action: '{action}', RunCondition: {runCondition}, Triggered: {triggered}";
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
                    if (!runCondition.Equals(RunCondition.Always))
                    {
                        var status = Connection.EvaluateExpression("state.status").Result.ToString().Trim().ToLower();
                        if (runCondition.Equals(RunCondition.WhilePrinting) && status != "processing"
                         || runCondition.Equals(RunCondition.NotPrinting) && status == "processing")
                        {
                            continue;
                        }
                    }

                    // evaluate the expression
                    try
                    {
                        var result = Connection.EvaluateExpression(stateExpression).Result.ToString();

                        // compare the result with the trigger value
                        bool conditionMet = stateOperation switch
                        {
                            "==" => result == (stateTriggerValue as string),
                            "!=" => result != (stateTriggerValue as string),
                            ">" => Convert.ToDouble(result) > Convert.ToDouble(stateTriggerValue),
                            "<" => Convert.ToDouble(result) < Convert.ToDouble(stateTriggerValue),
                            ">=" => Convert.ToDouble(result) >= Convert.ToDouble(stateTriggerValue),
                            "<=" => Convert.ToDouble(result) <= Convert.ToDouble(stateTriggerValue),
                            _ => throw new InvalidOperationException("Invalid state operation: " + stateOperation)
                        };

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
                        Console.WriteLine($"SBCTrigger: Failed to evaluate expression '{stateExpression}', deactivating the corresponding trigger for now: {e.Message}");
                        SetRunCondition(RunCondition.Disabled);
                        break;
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

        [System.Text.RegularExpressions.GeneratedRegex(@"^(.*?)(==|!=|>=|<=|>|<)(.*)$")]
        private static partial System.Text.RegularExpressions.Regex ExpressionRegex();
    }
}