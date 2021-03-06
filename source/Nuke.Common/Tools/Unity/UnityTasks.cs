﻿// Copyright 2019 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Unity.Logging;
using Nuke.Common.Utilities;

namespace Nuke.Common.Tools.Unity
{
    public partial class UnityTasks
    {
        [ThreadStatic]
        private static FileWatcher s_watcher;

        [ThreadStatic]
        private static LogParser s_logParser;

        [ThreadStatic]
        private static bool s_minimalOutput;

        public static string GetToolPath()
        {
            return EnvironmentInfo.IsWin
                ? EnvironmentInfo.SpecialFolder(SpecialFolders.ProgramFiles) + "/Editor/Unity.exe"
                : EnvironmentInfo.IsOsx
                    ? "/Applications/Unity/Unity.app/Contents/MacOS/Unity"
                    : null;
        }

        private static void PreProcess<T>(ref T unitySettings)
            where T : UnityBaseSettings
        {
            if (File.Exists(unitySettings.GetLogFile()))
                File.Delete(unitySettings.GetLogFile());
            s_minimalOutput = unitySettings.MinimalOutput.GetValueOrDefault(defaultValue: false);
            s_logParser = new LogParser(LogLine, LogBlockStart, LogBlockEnd);
            s_watcher = new FileWatcher(unitySettings.GetLogFile(), s_logParser.Log);
            s_watcher.Start();
        }

        [CanBeNull]
        private static IProcess StartProcess(UnityBaseSettings unitySettings)
        {
            try
            {
                return ProcessTasks.StartProcess(unitySettings);
            }
            catch (Exception)
            {
                AssertWatcherStopped();
                throw;
            }
        }

        private static void AssertWatcherStopped()
        {
            s_watcher?.AssertStopped();
            s_watcher = null;
        }

        private static void AssertProcess(IProcess process, UnityBaseSettings settings)
        {
            process.AssertWaitForExit();
            AssertWatcherStopped();
            if (process.ExitCode == 0)
                return;

            var message = new StringBuilder()
                .AppendLine($"Process '{Path.GetFileName(process.FileName)}' exited with code {process.ExitCode}. Verify the invocation.")
                .AppendLine($"> {process.FileName.DoubleQuoteIfNeeded()} {process.Arguments}")
                .ToString();

            if (settings.StableExitCodes.Any(x => x == process.ExitCode))
                Logger.Warn(message);
            else
                ControlFlow.Fail(message);
        }

        private static void LogLine(string message, Logging.LogLevel logLevel)
        {
            switch (logLevel)
            {
                case Logging.LogLevel.Normal:
                    if (!s_minimalOutput)
                        Logger.Info(message);
                    break;
                case Logging.LogLevel.Warning:
                    Logger.Warn(message);
                    break;
                case Logging.LogLevel.Error:
                case Logging.LogLevel.Failure:
                    Logger.Error(message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, message: null);
            }
        }

        private static void LogBlockEnd(MatchedBlock block)
        {
            Logger.Normal("End: " + block.Name.TrimEnd('\r', '\n'));
        }

        private static void LogBlockStart(MatchedBlock block)
        {
            Logger.Normal("Start: " + block.Name.TrimEnd('\r', '\n'));
        }
    }
}
