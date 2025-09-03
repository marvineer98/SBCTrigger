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
            var triggers = new Dictionary<uint, SBCTrigger>();

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
                        // get a list of all defined triggers if no params set
                        // define a new trigger or update an existing one if params are given
                        case 583 when code.MinorNumber is 0 or null:

                            // get the trigger index
                            code.TryGetUInt('T', out uint? index);
                            // get the state expression if any
                            code.TryGetString('P', out var stateExpression);
                            // get the action if any
                            code.TryGetString('A', out var action);
                            // get the initial trigger state if any
                            var initialState = code.GetBool('S', false);
                            // get the run condition
                            var runCondition = (RunCondition)code.GetInt('R', 0);

                            // if no params are given, list all defined triggers
                            if (index == null && stateExpression == null && action == null)
                            {
                                var summaryMessage = "No SBCTriggers defined.";
                                // list all defined triggers
                                if (triggers.Count > 0)
                                {
                                    summaryMessage = $"There are {triggers.Count} SBCTriggers defined:\n";
                                    foreach (var trigger in triggers.OrderBy(t => t.Key).ToList())
                                    {
                                        summaryMessage += "SBCTrigger " + trigger.Key + ": " + trigger.Value.ToString() + "\n";
                                    }
                                }
                                await Connection.ResolveCode(MessageType.Success, summaryMessage, CancellationToken);
                                break;
                            }
                            // if an index is given and the trigger is not known, create it
                            else if (index.HasValue && !triggers.TryGetValue(index.Value, out _))
                            {
                                if (!string.IsNullOrWhiteSpace(stateExpression) && !string.IsNullOrWhiteSpace(action))
                                {
                                    triggers[index.Value] = new SBCTrigger(stateExpression, action, initialState, runCondition);
                                    await Connection.ResolveCode(MessageType.Success, $"SBCTrigger {index.Value} created.", CancellationToken);
                                    break;
                                }
                            }
                            // if an index is given and there is a trigger, update it
                            else if (index.HasValue && triggers.TryGetValue(index.Value, out _))
                            {
                                // update an existing trigger (parcially)
                                triggers[index.Value].UpdateParams(stateExpression, action, runCondition);
                                await Connection.ResolveCode(MessageType.Success, $"SBCTrigger {index.Value} updated.", CancellationToken);
                                break;
                            }

                            await Connection.ResolveCode(MessageType.Error, $"Provide at least 'T' (trigger index), 'P' (expression) and 'A' (action) to create an SBCTrigger.", CancellationToken);
                            break;
                        
                        // Unknown code. Should never get here
                        default:
                            await Connection.IgnoreCode();
                            break;
                    }
                }
                catch (Exception e)
                {
                    await Connection.ResolveCode(MessageType.Error, $"{code.ToShortString()} failed: {e.Message}");
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