using ATT_Wrapper.Components;
using ATT_Wrapper.Models; // LogResult теперь здесь
using Serilog;
using System;
using System.Threading.Tasks;

namespace ATT_Wrapper.Services
    {
    public class UpdateChecker
        {
        private readonly IGitRunner _gitRunner;

        /// <summary>
        /// Создает чекер.
        /// Если runner не передан (null), используется реальный GitRunner.
        /// Это сохраняет совместимость с существующим кодом вызова (new UpdateChecker()).
        /// </summary>
        public UpdateChecker(IGitRunner runner = null)
            {
            _gitRunner = runner ?? new GitRunner();
            }

        public async Task<LogResult> IsUpdateAvailable()
            {
            return await Task.Run(() =>
            {
                try
                    {
                    // 1. fetch
                    _gitRunner.Run("fetch");

                    // 2. rev-list
                    string output = _gitRunner.Run("rev-list --count HEAD..@{u}");

                    if (int.TryParse(output, out int commitsBehind))
                        {
                        if (commitsBehind > 0)
                            {
                            return LogResult.Pass("Updates found");
                            }
                        return LogResult.Pass("No updates available");
                        }
                    else
                        {
                        Log.Warning($"Git returned unexpected output: {output}");
                        return LogResult.Fail("Git output parsing error");
                        }
                    }
                catch (Exception ex)
                    {
                    Log.Error(ex, "Update check failed");
                    return LogResult.Fail("Updates check failed");
                    }
            });
            }
        }
    }