using DuetAPI.ObjectModel;
using DuetAPI.Connection;

using DuetAPIClient;

using DuetAPI.Commands;
using DuetAPI;

namespace SBCTrigger
{
    public static partial class Program
    {
        // codes that should be intercepted by this plugin
        public static readonly string[] InterceptedCodes = [
            "M583",
            "M583.1"
        ];

        // Connection used for intercepting codes from stream
        public static InterceptConnection Connection { get; } = new();

        // Global cancellation source that is triggered when the program is supposed to terminate
        public static readonly CancellationTokenSource CancelSource = new();
        public static readonly CancellationToken CancellationToken = CancelSource.Token;

        // Main entry point
        static async Task Main(string[] args)
        {
            // Deal with program termination requests (SIGTERM and Ctrl+C)
            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                if (!CancelSource.IsCancellationRequested)
                {
                    CancelSource.Cancel();
                }
            };
            Console.CancelKeyPress += (sender, e) =>
            {
                if (!CancelSource.IsCancellationRequested)
                {
                    e.Cancel = true;
                    CancelSource.Cancel();
                }
            };

            // Create an intercepting connection for codes that are not supported natively by DCS
            await Connection.Connect(InterceptionMode.Pre, null, InterceptedCodes, false, Defaults.FullSocketPath);

            // load config file
            _ = Task.Run(LoadConfigFile);

            // store all managed Triggers with there number
            var triggers = new Dictionary<int, SBCTrigger>();

            // Keep intercepting codes until the plugin is stopped
            do
            {
                // Wait for the next code
                Code code;
                try
                {
                    code = await Connection.ReceiveCode(CancellationToken);

                    // Don't process system codes that need to go straight to the firmware
                    if (code.Flags.HasFlag(CodeFlags.IsInternallyProcessed))
                    {
                        await Connection.IgnoreCode();
                        continue;
                    }
                }
                catch (Exception e) when (e is OperationCanceledException)
                {
                    // Plugin is supposed to be terminated, stop here
                    break;
                }

                // Process the code
                try
                {
                    switch (code.MajorNumber)
                    {
                        // get a list of all defined triggers
                        case 583 when code.MinorNumber == 0 || code.MinorNumber == null:
                            if (triggers.Count == 0)
                            {
                                await Connection.ResolveCode(MessageType.Success, "No SBCTriggers defined. Define one with M583.1", CancellationToken);
                                break;
                            }

                            var summaryMessage = "Defined SBCTriggers:\n";
                            foreach (var trigger in triggers)
                            {
                                summaryMessage += "Trigger " + trigger.Key + ": " + trigger.Value.ToString() + "\n";
                            }
                            await Connection.ResolveCode(MessageType.Success, summaryMessage, CancellationToken);
                            break;
                        // Set or update a spedivic SBCTrigger
                        case 583 when code.MinorNumber == 1:
                            try
                            {
                                // get the trigger index
                                var index = code.GetInt('T');
                                // get the state expression if any
                                code.TryGetString('P', out var stateExpression);
                                // get the action if any
                                code.TryGetString('A', out var action);
                                // get the run condition
                                var runCondition = (RunCondition)code.GetInt('R', 0);

                                // create a new trigger if it does not exist yet
                                if (!triggers.TryGetValue(index, out var trigger))
                                {
                                    if (string.IsNullOrWhiteSpace(stateExpression) || string.IsNullOrWhiteSpace(action))
                                    {
                                        await Connection.ResolveCode(MessageType.Error, $"SBCTrigger {index} does not exist. Provide both 'P' and 'A' to create.", CancellationToken);
                                        break;
                                    }
                                    triggers[index] = new SBCTrigger(stateExpression, action, runCondition);
                                    await Connection.ResolveCode(MessageType.Success, $"SBCTrigger {index} created.", CancellationToken);
                                    break;
                                }

                                // update an existing trigger runCondition
                                if (string.IsNullOrWhiteSpace(stateExpression) || string.IsNullOrWhiteSpace(action))
                                {
                                    triggers[index].SetRunCondition(runCondition);
                                    await Connection.ResolveCode(MessageType.Success, $"SBCTrigger {index} run condition updated.", CancellationToken);
                                    break;
                                }

                                // update an existing trigger completely
                                triggers[index].SetRunCondition(RunCondition.Disabled);
                                triggers[index] = new SBCTrigger(stateExpression, action, runCondition);
                                await Connection.ResolveCode(MessageType.Success, $"SBCTrigger {index} updated.", CancellationToken);
                                break;
                            }
                            catch (Exception e)
                            {
                                await Connection.ResolveCode(MessageType.Error, $"Failed to perform action: {e.Message}", CancellationToken);
                            }
                            break;
                        // Unknown code. Should never get here
                        default:
                            await Connection.IgnoreCode();
                            break;
                    }
                }
                catch (Exception e) when (e is MissingParameterException or InvalidParameterTypeException)
                {
                    await Connection.ResolveCode(MessageType.Error, $"{code.ToShortString()}: {e.Message}");
                }
            }
            while (!CancellationToken.IsCancellationRequested);
        }

        // Command connection used to send commands outside of the interception context
        // This is needed to load the plugin config.g file
        static async Task LoadConfigFile()
        {
            CommandConnection CommandConnection = new();
            await CommandConnection.Connect();
            await CommandConnection.PerformSimpleCode("M98 P\"SBCTrigger_config.g\"");
            CommandConnection.Dispose();
        }
    }
}