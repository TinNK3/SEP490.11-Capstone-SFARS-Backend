using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFARS.Application.Common;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Infrastructure.Configurations;
using System.Diagnostics;
using System.Text.Json;

namespace SFARS.Application.Services;

public class RetrainOrchestrationService : IRetrainOrchestrationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RetrainOrchestrationService> _logger;
    private readonly IFileStorageService _fileStorageService;
    private readonly ISpeciesClassificationService _classificationService;
    private readonly ISystemMessageService _msgService;
    private readonly MlopsOptions _mlopsOptions;

    public RetrainOrchestrationService(
        IUnitOfWork unitOfWork,
        ILogger<RetrainOrchestrationService> logger,
        IFileStorageService fileStorageService,
        ISpeciesClassificationService classificationService,
        ISystemMessageService messageService,
        IOptions<MlopsOptions> options)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _fileStorageService = fileStorageService;
        _classificationService = classificationService;
        _msgService = messageService;
        _mlopsOptions = options.Value;
    }

    public async Task<IServiceResult> TriggerRetrainAsync(DateTime? since = null)
    {
        _logger.LogInformation("Starting AI Retrain Orchestration...");
        
        var history = new RetrainHistory
        {
            Id = Guid.NewGuid(),
            Status = RetrainStatus.Pending,
            StartedAt = DateTime.UtcNow
        };
        
        await _unitOfWork.Repository<RetrainHistory, Guid>().AddAsync(history);
        await _unitOfWork.SaveChangesAsync();
        
        try
        {
            history.Status = RetrainStatus.Training;
            await _unitOfWork.SaveChangesAsync();

            var runResult = await ExecutePythonMLOpsPipelineAsync();

            history.ModelVersion = runResult.ModelVersion;
            history.TotalSamplesProcessed = runResult.SampleCount;
            history.IsPromoted = runResult.IsPromoted;
            history.OldAccuracy = runResult.OldAccuracy;
            history.NewAccuracy = runResult.NewAccuracy;

            if (runResult.Success)
            {
                if (runResult.IsPromoted)
                {
                    _logger.LogInformation("Model was promoted! Hot swapping ONNX model...");
                    bool hotSwapSuccess = await _classificationService.ReloadSpeciesModelAsync();
                    if (!hotSwapSuccess)
                    {
                        history.ErrorMessage = "Training succeeded and model promoted, but hot-swap failed in backend.";
                        _logger.LogError(history.ErrorMessage);
                    }
                }
                
                history.Status = RetrainStatus.Success;
            }
            else
            {
                history.Status = RetrainStatus.Failed;
                history.ErrorMessage = runResult.ErrorMessage ?? "Python script returned failure exit code";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during AI Retrain Orchestration");
            history.Status = RetrainStatus.Failed;
            history.ErrorMessage = ex.Message;
        }
        finally
        {
            history.CompletedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }

        return new ServiceResult(ResultCodeConst.SYS_Success0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001));
    }

    public async Task<IServiceResult> GetRetrainHistoryAsync(int count = 10)
    {
        var history = await _unitOfWork.Repository<RetrainHistory, Guid>().GetQueryable(tracked: false)
            .OrderByDescending(h => h.StartedAt)
            .Take(count)
            .ToListAsync();
            
        return new ServiceResult(ResultCodeConst.SYS_Success0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001), history);
    }

    private async Task<PythonRunResult> ExecutePythonMLOpsPipelineAsync()
    {
        var result = new PythonRunResult();
        
        string pythonExecutable = _mlopsOptions.PythonExecutable;
        string scriptDir = ResolveScriptDirectory();
        string scriptPath = Path.Combine(scriptDir, _mlopsOptions.PipelineScriptName);

        if (!File.Exists(scriptPath))
        {
            result.Success = false;
            result.ErrorMessage = $"Could not find MLOps script. Expected at: {_mlopsOptions.ScriptsRelativePath}/{_mlopsOptions.PipelineScriptName}";
            return result;
        }

        _logger.LogInformation("Executing MLOps pipeline at: {ScriptPath}", scriptPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = $"\"{scriptPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = scriptDir
        };

        using var process = new Process { StartInfo = startInfo };
        
        string outputLog = string.Empty;
        process.OutputDataReceived += (s, e) => { if (e.Data != null) outputLog += e.Data + "\n"; };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) _logger.LogWarning("MLOps STDERR: {Data}", e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var timeout = TimeSpan.FromMinutes(_mlopsOptions.PipelineTimeoutMinutes);
        await process.WaitForExitAsync(new CancellationTokenSource(timeout).Token);

        _logger.LogInformation("MLOps pipeline completed with exit code: {ExitCode}", process.ExitCode);

        // Exit codes: 0 = Promoted, 1 = Kept Champion, 2+ = Error
        if (process.ExitCode == 0 || process.ExitCode == 1)
        {
            result.Success = true;
            result.IsPromoted = (process.ExitCode == 0);
            
            try 
            {
                var lines = outputLog.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var lastLine = lines.LastOrDefault(l => l.Trim().StartsWith("{") && l.Trim().EndsWith("}"));
                
                if (!string.IsNullOrEmpty(lastLine))
                {
                    var report = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(lastLine);
                    if (report != null)
                    {
                        if (report.TryGetValue("model_version", out var mv)) result.ModelVersion = mv.GetString();
                        if (report.TryGetValue("samples", out var samp)) result.SampleCount = samp.GetInt32();
                        if (report.TryGetValue("old_accuracy", out var oAcc)) result.OldAccuracy = oAcc.GetDouble();
                        if (report.TryGetValue("new_accuracy", out var nAcc)) result.NewAccuracy = nAcc.GetDouble();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not parse metadata from Python output. Raw output: {Output}", outputLog);
            }
        }
        else
        {
            result.Success = false;
            int maxLen = _mlopsOptions.MaxErrorLogLength;
            result.ErrorMessage = $"Python script failed with exit code {process.ExitCode}. Log: {outputLog[Math.Max(0, outputLog.Length - maxLen)..]}";
        }

        return result;
    }

    private string ResolveScriptDirectory()
    {
        string currentDir = AppContext.BaseDirectory;
        string targetRelativeDir = _mlopsOptions.ScriptsRelativePath; // "scripts/mlops"

        // Traverse upwards until we find the "scripts" folder in the same directory
        // This handles cases whether running from bin/Debug/net9.0, or from project root
        while (!string.IsNullOrEmpty(currentDir))
        {
            string candidate = Path.GetFullPath(Path.Combine(currentDir, targetRelativeDir));
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            // Move one level up
            var parent = Directory.GetParent(currentDir);
            if (parent == null) break;
            currentDir = parent.FullName;
        }

        // Fallback: If not found, return a relative path from current execution dir 
        // to provide a clear error path in logs.
        _logger.LogWarning("Could not resolve absolute path for script directory. Using fallback.");
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), _mlopsOptions.ScriptsRelativePath));
    }
    
    private class PythonRunResult
    {
        public bool Success { get; set; }
        public bool IsPromoted { get; set; }
        public string? ModelVersion { get; set; }
        public int SampleCount { get; set; }
        public double? OldAccuracy { get; set; }
        public double? NewAccuracy { get; set; }
        public string? ErrorMessage { get; set; }
    }
}