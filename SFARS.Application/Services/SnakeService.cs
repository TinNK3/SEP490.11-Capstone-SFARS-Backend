using ClosedXML.Excel;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Application.Services
{
    public class SnakeService : GenericService<Snake, SnakeDto, Guid>, ISnakeService<SnakeDto>
    {
        // Sensitive fields that require ChangeReason when modified
        private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(Snake.ToxicityLevel),
            nameof(Snake.ToxinGroup)
        };

        private readonly IFileStorageService _storageService;

        public SnakeService(
            ISystemMessageService msgService,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<SnakeService> logger,
            IFileStorageService storageService)
            : base(msgService, unitOfWork, mapper, logger)
        {
            _storageService = storageService;
        }

        #region CRUD

        /// <summary>
        /// Get snake by ID
        /// </summary>
        public async Task<IServiceResult> GetSnakeById(Guid id)
        {
            var spec = new SnakeSpecification(id);
            var result = await GetWithSpecAsync(spec);

            if (result.Data == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
            }

            return result;
        }

        /// <summary>
        /// Create a new snake with ScientificName uniqueness check + audit log
        /// </summary>
        public override async Task<IServiceResult> CreateAsync(SnakeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ScientificName))
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));
            }

            if (string.IsNullOrWhiteSpace(dto.CommonName))
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));
            }

            // Check uniqueness by ScientificName
            var isDuplicate = await _unitOfWork.Repository<Snake, Guid>()
                .AnyAsync(s => s.ScientificName == dto.ScientificName.Trim());

            if (isDuplicate)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0003,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0003));
            }

            // Create via base
            var result = await base.CreateAsync(dto);

            // Log creation audit
            if (result.ResultCode == ResultCodeConst.SYS_Success0001)
            {
                var trimmedName = dto.ScientificName.Trim();
                var allSnakes = await _unitOfWork.Repository<Snake, Guid>().GetAllAsync(false);
                var snakeEntity = allSnakes.FirstOrDefault(s => s.ScientificName == trimmedName);

                if (snakeEntity != null)
                {
                    var logs = BuildCreateLogs(snakeEntity);
                    await _unitOfWork.Repository<SnakeChangeLog, Guid>().AddRangeAsync(logs);
                    await _unitOfWork.SaveChangesAsync();
                }
            }

            return result;
        }

        // Compatibility wrapper
        public async Task<IServiceResult> CreateSnake(SnakeDto dto)
        {
            return await CreateAsync(dto);
        }

        /// <summary>
        /// Update snake with field-level change detection and audit logging.
        /// </summary>
        public async Task<IServiceResult> UpdateSnakeAsync(Guid id, SnakeDto dto, string? changeReason)
        {
            var snake = await _unitOfWork.Repository<Snake, Guid>().GetByIdAsync(id);
            if (snake == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
            }

            // Detect field-level changes BEFORE applying update
            var changeLogs = DetectFieldChanges(snake, dto, changeReason);

            // Check if sensitive fields changed without reason
            if (string.IsNullOrWhiteSpace(changeReason))
            {
                var sensitiveChanges = changeLogs.Where(c => SensitiveFields.Contains(c.FieldName)).ToList();
                if (sensitiveChanges.Any())
                {
                    var fields = string.Join(", ", sensitiveChanges.Select(c => c.FieldName));
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0001,
                        $"ChangeReason is required when modifying sensitive fields: {fields}");
                }
            }

            if (!changeLogs.Any())
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Success0003,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003))
                {
                    Data = true
                };
            }

            // Apply updates (explicit assignment — avoid Mapster overwriting PK)
            snake.CommonName = dto.CommonName;
            snake.ScientificName = dto.ScientificName;
            snake.ToxicityLevel = dto.ToxicityLevel;
            snake.ToxinGroup = dto.ToxinGroup;
            snake.Description = dto.Description;
            snake.KeyIdentifiers = dto.KeyIdentifiers;
            snake.TypicalSymptoms = dto.TypicalSymptoms;
            snake.Habitat = dto.Habitat;
            snake.DistributionNote = dto.DistributionNote;
            snake.Note = dto.Note;
            snake.IsActive = dto.IsActive;
            snake.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Repository<Snake, Guid>().UpdateAsync(snake);

            // Save audit logs
            await _unitOfWork.Repository<SnakeChangeLog, Guid>().AddRangeAsync(changeLogs);
            await _unitOfWork.SaveChangesAsync();

            // Build warning message for sensitive field changes
            var sensitiveChanged = changeLogs.Where(c => SensitiveFields.Contains(c.FieldName)).ToList();
            var message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003);
            if (sensitiveChanged.Any())
            {
                var warnings = sensitiveChanged.Select(c => $"{c.FieldName}: {c.OldValue} → {c.NewValue}");
                message += $" | ⚠️ Sensitive fields changed: {string.Join("; ", warnings)}";
            }

            return new ServiceResult(ResultCodeConst.SYS_Success0003, message) { Data = true };
        }

        /// <summary>
        /// Soft delete snake by ID with audit logging
        /// </summary>
        public async Task<IServiceResult> DeleteSnake(Guid id)
        {
            var snake = await _unitOfWork.Repository<Snake, Guid>().GetByIdAsync(id);

            if (snake == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
            }

            // Soft delete
            snake.IsActive = false;
            snake.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Repository<Snake, Guid>().UpdateAsync(snake);

            // Audit log
            var log = new SnakeChangeLog
            {
                Id = Guid.NewGuid(),
                SnakeId = snake.Id,
                FieldName = nameof(Snake.IsActive),
                OldValue = "true",
                NewValue = "false",
                ChangeType = "SoftDelete",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<SnakeChangeLog, Guid>().AddAsync(log);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult(
                ResultCodeConst.SYS_Success0004,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0004));
        }

        #endregion

        #region Search

        public async Task<IServiceResult> GetAllSnakesAsync(SnakeSpecParams specParams)
        {
            var spec = new SnakeSpecification(specParams, isCount: false);
            var result = await GetAllWithSpecAsync(spec);

            var countSpec = new SnakeSpecification(specParams, isCount: true);
            var totalItems = await _unitOfWork.Repository<Snake, Guid>().CountAsync(countSpec);

            var limit = specParams.GetTake();
            var page = specParams.GetPage();
            var totalPages = limit > 0 ? (int)Math.Ceiling(totalItems / (double)limit) : 0;

            var dtos = result.Data as IEnumerable<SnakeDto> ?? Enumerable.Empty<SnakeDto>();

            var pagedResult = new PaginatedResultDto<SnakeDto>(
                dtos,
                page,
                limit,
                totalPages,
                totalItems
            );

            result.Data = pagedResult;
            return result;
        }

        #endregion

        #region Excel Import

        /// <summary>
        /// Preview Excel (.xlsx) import: parse, validate, return summary of what would happen — no DB write.
        /// </summary>
        public async Task<IServiceResult> PreviewImportAsync(Stream excelStream)
        {
            var (records, errors) = ParseExcel(excelStream);

            if (errors.Any())
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0001,
                    "Excel file has validation errors")
                {
                    Data = new { Errors = errors, TotalRows = records.Count + errors.Count }
                };
            }

            // Build preview
            var existingSnakes = (await _unitOfWork.Repository<Snake, Guid>().GetAllAsync(false))
                .ToDictionary(s => s.ScientificName, s => s);

            var toInsert = new List<string>();
            var toUpdate = new List<string>();
            var toSkip = new List<string>();

            foreach (var dto in records)
            {
                if (existingSnakes.TryGetValue(dto.ScientificName, out var existing))
                {
                    var changes = DetectFieldChanges(existing, dto, null);
                    if (changes.Any())
                        toUpdate.Add($"{dto.ScientificName} ({dto.CommonName}) — {changes.Count} field(s)");
                    else
                        toSkip.Add($"{dto.ScientificName} ({dto.CommonName})");
                }
                else
                {
                    toInsert.Add($"{dto.ScientificName} ({dto.CommonName})");
                }
            }

            return new ServiceResult(ResultCodeConst.SYS_Success0001, "Import preview ready")
            {
                Data = new
                {
                    TotalRows = records.Count,
                    Insert = toInsert.Count,
                    Update = toUpdate.Count,
                    Skip = toSkip.Count,
                    InsertDetails = toInsert,
                    UpdateDetails = toUpdate,
                    SkipDetails = toSkip
                }
            };
        }

        /// <summary>
        /// Apply Excel (.xlsx) import: upsert by ScientificName with full audit logging.
        /// </summary>
        public async Task<IServiceResult> ApplyImportAsync(Stream excelStream, string? changeReason)
        {
            var (records, errors) = ParseExcel(excelStream);

            if (errors.Any())
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0001,
                    "Excel file has validation errors")
                {
                    Data = new { Errors = errors }
                };
            }

            var existingSnakes = (await _unitOfWork.Repository<Snake, Guid>().GetAllAsync())
                .ToDictionary(s => s.ScientificName, s => s);

            var now = DateTime.UtcNow;
            var insertedCount = 0;
            var updatedCount = 0;
            var skippedCount = 0;
            var allLogs = new List<SnakeChangeLog>();

            foreach (var dto in records)
            {
                if (existingSnakes.TryGetValue(dto.ScientificName, out var existing))
                {
                    var changeLogs = DetectFieldChanges(existing, dto, changeReason ?? "Excel Import");
                    if (changeLogs.Any())
                    {
                        // Apply update
                        existing.CommonName = dto.CommonName;
                        existing.ToxicityLevel = dto.ToxicityLevel;
                        existing.ToxinGroup = dto.ToxinGroup;
                        existing.Description = dto.Description;
                        existing.KeyIdentifiers = dto.KeyIdentifiers;
                        existing.TypicalSymptoms = dto.TypicalSymptoms;
                        existing.Habitat = dto.Habitat;
                        existing.DistributionNote = dto.DistributionNote;
                        existing.Note = dto.Note;
                        existing.UpdatedAt = now;

                        await _unitOfWork.Repository<Snake, Guid>().UpdateAsync(existing);
                        allLogs.AddRange(changeLogs);
                        updatedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                else
                {
                    // Insert
                    var entity = _mapper.Map<Snake>(dto);
                    entity.Id = Guid.NewGuid();
                    entity.CreatedAt = now;
                    entity.IsActive = true;
                    await _unitOfWork.Repository<Snake, Guid>().AddAsync(entity);

                    allLogs.AddRange(BuildCreateLogs(entity, "Import"));
                    insertedCount++;
                }
            }

            // Save audit logs
            if (allLogs.Any())
            {
                await _unitOfWork.Repository<SnakeChangeLog, Guid>().AddRangeAsync(allLogs);
            }

            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult(ResultCodeConst.SYS_Success0002,
                $"Import completed. Inserted: {insertedCount}, Updated: {updatedCount}, Skipped: {skippedCount}")
            {
                Data = new { Inserted = insertedCount, Updated = updatedCount, Skipped = skippedCount }
            };
        }

        /// <summary>
        /// Update snake images (keep old, delete removed, upload new, manage primary).
        /// </summary>
        public async Task<IServiceResult> UpdateSnakeImagesAsync(
            Guid snakeId, 
            List<Guid>? keepImageIds, 
            Guid? primaryExistingImageId, 
            List<SnakeImageUploadInfo>? newImages, 
            int? primaryNewImageIndex)
        {
            var spec = new SnakeSpecification(snakeId);
            var snake = await _unitOfWork.Repository<Snake, Guid>().GetWithSpecAsync(spec);

            if (snake == null)
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0004, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
            }

            keepImageIds ??= new List<Guid>();

            // 1. Identify and delete images to remove
            var imagesToRemove = snake.SnakeImages
                .Where(img => !keepImageIds.Contains(img.Id))
                .ToList();

            foreach (var img in imagesToRemove)
            {
                // Delete from Cloudinary
                await _storageService.DeleteByUrlAsync(img.ImageUrl);
                // Remove from collection
                snake.SnakeImages.Remove(img);
                // Explicitly delete from repository by ID
                await _unitOfWork.Repository<SnakeImage, Guid>().DeleteAsync(img.Id);
            }

            // 2. Upload and add new images
            if (newImages != null && newImages.Any())
            {
                foreach (var uploadInfo in newImages)
                {
                    var uploadResult = await _storageService.UploadAsync(
                        uploadInfo.Stream, 
                        uploadInfo.FileName, 
                        "snakes", 
                        uploadInfo.ContentType);
                    
                    var newImg = new SnakeImage
                    {
                        Id = Guid.NewGuid(),
                        SnakeId = snakeId,
                        ImageUrl = uploadResult.Url,
                        IsPrimary = false
                    };
                    
                    snake.SnakeImages.Add(newImg);
                    await _unitOfWork.Repository<SnakeImage, Guid>().AddAsync(newImg);
                }
            }

            // 3. Handle IsPrimary logic
            // Reset all to false first
            foreach (var img in snake.SnakeImages)
            {
                img.IsPrimary = false;
            }

            bool primarySet = false;

            // Priority 1: Specified existing image
            if (primaryExistingImageId.HasValue)
            {
                var existingPrimary = snake.SnakeImages.FirstOrDefault(img => img.Id == primaryExistingImageId.Value);
                if (existingPrimary != null)
                {
                    existingPrimary.IsPrimary = true;
                    primarySet = true;
                }
            }

            // Priority 2: Specified new image index (if primary not set yet)
            if (!primarySet && primaryNewImageIndex.HasValue && newImages != null)
            {
                // New images were added to snake.SnakeImages at the end.
                // We uploaded them in order, so they are the last N items.
                int newImageCount = newImages.Count;
                if (primaryNewImageIndex >= 0 && primaryNewImageIndex < newImageCount)
                {
                    var newlyAddedImages = snake.SnakeImages.TakeLast(newImageCount).ToList();
                    newlyAddedImages[primaryNewImageIndex.Value].IsPrimary = true;
                    primarySet = true;
                }
            }

            // Final fallback: Ensure at least one image is primary if any images exist
            if (!primarySet && snake.SnakeImages.Any())
            {
                snake.SnakeImages.First().IsPrimary = true;
            }

            snake.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult(ResultCodeConst.SYS_Success0001, "Snake images updated successfully.");
        }

        #endregion

        #region History & Revert

        /// <summary>
        /// Get change history for a specific snake with pagination.
        /// </summary>
        public async Task<IServiceResult> GetSnakeChangeHistory(Guid snakeId, BaseSpecParams specParams)
        {
            var snake = await _unitOfWork.Repository<Snake, Guid>().GetByIdAsync(snakeId);
            if (snake == null)
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0004, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
            }

            var allLogs = (await _unitOfWork.Repository<SnakeChangeLog, Guid>().GetAllAsync(false))
                .Where(l => l.SnakeId == snakeId)
                .OrderByDescending(l => l.CreatedAt)
                .ToList();

            var totalItems = allLogs.Count;
            var limit = specParams.GetTake();
            var page = specParams.GetPage();
            var skip = specParams.GetSkip();

            var paged = allLogs.Skip(skip).Take(limit).ToList();
            var dtos = _mapper.Map<List<SnakeChangeLogDto>>(paged);

            var totalPages = limit > 0 ? (int)Math.Ceiling(totalItems / (double)limit) : 0;
            var pagedResult = new PaginatedResultDto<SnakeChangeLogDto>(
                dtos,
                page,
                limit,
                totalPages,
                totalItems
            );

            return new ServiceResult(ResultCodeConst.SYS_Success0001, "Change history retrieved")
            {
                Data = pagedResult
            };
        }

        /// <summary>
        /// Revert a specific field change using its ChangeLog entry ID.
        /// Creates a new "Revert" audit entry.
        /// </summary>
        public async Task<IServiceResult> RevertSnakeField(Guid changeLogId)
        {
            var changeLog = await _unitOfWork.Repository<SnakeChangeLog, Guid>().GetByIdAsync(changeLogId);
            if (changeLog == null)
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0004, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
            }

            var snake = await _unitOfWork.Repository<Snake, Guid>().GetByIdAsync(changeLog.SnakeId);
            if (snake == null)
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0004, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
            }

            // Get current value of the field
            var currentValue = GetFieldValue(snake, changeLog.FieldName);
            var revertToValue = changeLog.OldValue;

            // Apply revert
            SetFieldValue(snake, changeLog.FieldName, revertToValue);
            snake.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Repository<Snake, Guid>().UpdateAsync(snake);

            // Log the revert itself as a new audit entry
            var revertLog = new SnakeChangeLog
            {
                Id = Guid.NewGuid(),
                SnakeId = snake.Id,
                FieldName = changeLog.FieldName,
                OldValue = currentValue,
                NewValue = revertToValue,
                ChangeType = "Revert",
                ChangeReason = $"Reverted from ChangeLog #{changeLog.Id}",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<SnakeChangeLog, Guid>().AddAsync(revertLog);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult(ResultCodeConst.SYS_Success0003,
                $"Reverted {changeLog.FieldName}: '{currentValue}' → '{revertToValue}'")
            {
                Data = true
            };
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Compare all fields between existing entity and incoming DTO, return list of change logs.
        /// </summary>
        private List<SnakeChangeLog> DetectFieldChanges(Snake existing, SnakeDto dto, string? changeReason)
        {
            var logs = new List<SnakeChangeLog>();
            var now = DateTime.UtcNow;

            void Check(string fieldName, string? oldVal, string? newVal)
            {
                if (!string.Equals(oldVal?.Trim(), newVal?.Trim(), StringComparison.Ordinal))
                {
                    logs.Add(new SnakeChangeLog
                    {
                        Id = Guid.NewGuid(),
                        SnakeId = existing.Id,
                        FieldName = fieldName,
                        OldValue = oldVal,
                        NewValue = newVal,
                        ChangeType = "Update",
                        ChangeReason = changeReason,
                        CreatedAt = now
                    });
                }
            }

            Check(nameof(Snake.CommonName), existing.CommonName, dto.CommonName);
            Check(nameof(Snake.ScientificName), existing.ScientificName, dto.ScientificName);
            Check(nameof(Snake.ToxicityLevel), existing.ToxicityLevel.ToString(), dto.ToxicityLevel.ToString());
            Check(nameof(Snake.ToxinGroup), existing.ToxinGroup.ToString(), dto.ToxinGroup.ToString());
            Check(nameof(Snake.Description), existing.Description, dto.Description);
            Check(nameof(Snake.KeyIdentifiers), existing.KeyIdentifiers, dto.KeyIdentifiers);
            Check(nameof(Snake.TypicalSymptoms), existing.TypicalSymptoms, dto.TypicalSymptoms);
            Check(nameof(Snake.Habitat), existing.Habitat, dto.Habitat);
            Check(nameof(Snake.DistributionNote), existing.DistributionNote, dto.DistributionNote);
            Check(nameof(Snake.Note), existing.Note, dto.Note);

            return logs;
        }

        /// <summary>
        /// Build audit logs for a newly created snake entity.
        /// </summary>
        private List<SnakeChangeLog> BuildCreateLogs(Snake snake, string changeType = "Create")
        {
            var now = DateTime.UtcNow;
            var fields = new Dictionary<string, string?>
            {
                [nameof(Snake.ScientificName)] = snake.ScientificName,
                [nameof(Snake.CommonName)] = snake.CommonName,
                [nameof(Snake.ToxicityLevel)] = snake.ToxicityLevel.ToString(),
                [nameof(Snake.ToxinGroup)] = snake.ToxinGroup.ToString(),
                [nameof(Snake.Description)] = snake.Description,
                [nameof(Snake.KeyIdentifiers)] = snake.KeyIdentifiers,
                [nameof(Snake.TypicalSymptoms)] = snake.TypicalSymptoms,
                [nameof(Snake.Habitat)] = snake.Habitat,
                [nameof(Snake.DistributionNote)] = snake.DistributionNote,
                [nameof(Snake.Note)] = snake.Note,
            };

            return fields.Where(f => !string.IsNullOrWhiteSpace(f.Value))
                .Select(f => new SnakeChangeLog
                {
                    Id = Guid.NewGuid(),
                    SnakeId = snake.Id,
                    FieldName = f.Key,
                    OldValue = null,
                    NewValue = f.Value,
                    ChangeType = changeType,
                    CreatedAt = now
                }).ToList();
        }

        /// <summary>
        /// Parse Excel (.xlsx) into List of SnakeDto with validation errors.
        /// Reads the first worksheet; header row must match field names.
        /// </summary>
        private (List<SnakeDto> Records, List<string> Errors) ParseExcel(Stream excelStream)
        {
            var records = new List<SnakeDto>();
            var errors = new List<string>();

            using var workbook = new XLWorkbook(excelStream);
            var worksheet = workbook.Worksheets.First();

            // Build header map from row 1
            var headerRow = worksheet.Row(1);
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var col = 1; col <= headerRow.LastCellUsed()?.Address.ColumnNumber; col++)
            {
                var headerName = headerRow.Cell(col).GetString().Trim();
                if (!string.IsNullOrWhiteSpace(headerName))
                    headerMap[headerName] = col;
            }

            // Validate required headers exist
            var requiredHeaders = new[] { "ScientificName", "CommonName", "ToxicityLevel", "ToxinGroup" };
            foreach (var h in requiredHeaders)
            {
                if (!headerMap.ContainsKey(h))
                    errors.Add($"Missing required column header: '{h}'");
            }
            if (errors.Any()) return (records, errors);

            // Helper to safely read a cell value
            string? CellValue(IXLRow row, string columnName)
            {
                if (!headerMap.TryGetValue(columnName, out var col)) return null;
                var val = row.Cell(col).GetString().Trim();
                return string.IsNullOrWhiteSpace(val) ? null : val;
            }

            // Read data rows (starting from row 2)
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var rowIdx = 2; rowIdx <= lastRow; rowIdx++)
            {
                var row = worksheet.Row(rowIdx);

                // Skip completely empty rows
                if (row.IsEmpty()) continue;

                try
                {
                    var scientificName = CellValue(row, "ScientificName");
                    var commonName = CellValue(row, "CommonName");
                    var toxicityLevelStr = CellValue(row, "ToxicityLevel");
                    var toxinGroupStr = CellValue(row, "ToxinGroup");

                    if (string.IsNullOrWhiteSpace(scientificName))
                    {
                        errors.Add($"Row {rowIdx}: ScientificName is required");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(commonName))
                    {
                        errors.Add($"Row {rowIdx}: CommonName is required");
                        continue;
                    }

                    if (!Enum.TryParse<SnakeRiskLevel>(toxicityLevelStr, true, out var riskLevel))
                    {
                        errors.Add($"Row {rowIdx}: Invalid ToxicityLevel '{toxicityLevelStr}'");
                        continue;
                    }

                    if (!Enum.TryParse<ToxinGroup>(toxinGroupStr, true, out var toxinGroup))
                    {
                        errors.Add($"Row {rowIdx}: Invalid ToxinGroup '{toxinGroupStr}'");
                        continue;
                    }

                    records.Add(new SnakeDto
                    {
                        ScientificName = scientificName.Trim(),
                        CommonName = commonName.Trim(),
                        ToxicityLevel = riskLevel,
                        ToxinGroup = toxinGroup,
                        Description = CellValue(row, "Description"),
                        KeyIdentifiers = CellValue(row, "KeyIdentifiers"),
                        TypicalSymptoms = CellValue(row, "TypicalSymptoms"),
                        Habitat = CellValue(row, "Habitat"),
                        DistributionNote = CellValue(row, "DistributionNote"),
                        Note = CellValue(row, "Note"),
                        IsActive = true
                    });
                }
                catch (Exception ex)
                {
                    errors.Add($"Row {rowIdx}: {ex.Message}");
                }
            }

            return (records, errors);
        }

        /// <summary>
        /// Get current value of a snake field by name (for revert).
        /// </summary>
        private string? GetFieldValue(Snake snake, string fieldName)
        {
            return fieldName switch
            {
                nameof(Snake.ScientificName) => snake.ScientificName,
                nameof(Snake.CommonName) => snake.CommonName,
                nameof(Snake.ToxicityLevel) => snake.ToxicityLevel.ToString(),
                nameof(Snake.ToxinGroup) => snake.ToxinGroup.ToString(),
                nameof(Snake.Description) => snake.Description,
                nameof(Snake.KeyIdentifiers) => snake.KeyIdentifiers,
                nameof(Snake.TypicalSymptoms) => snake.TypicalSymptoms,
                nameof(Snake.Habitat) => snake.Habitat,
                nameof(Snake.DistributionNote) => snake.DistributionNote,
                nameof(Snake.Note) => snake.Note,
                nameof(Snake.IsActive) => snake.IsActive.ToString(),
                _ => null
            };
        }

        /// <summary>
        /// Set a snake field by name (for revert). Handles enum parsing.
        /// </summary>
        private void SetFieldValue(Snake snake, string fieldName, string? value)
        {
            switch (fieldName)
            {
                case nameof(Snake.ScientificName): snake.ScientificName = value ?? ""; break;
                case nameof(Snake.CommonName): snake.CommonName = value ?? ""; break;
                case nameof(Snake.ToxicityLevel):
                    if (Enum.TryParse<SnakeRiskLevel>(value, out var rl)) snake.ToxicityLevel = rl;
                    break;
                case nameof(Snake.ToxinGroup):
                    if (Enum.TryParse<ToxinGroup>(value, out var tg)) snake.ToxinGroup = tg;
                    break;
                case nameof(Snake.Description): snake.Description = value; break;
                case nameof(Snake.KeyIdentifiers): snake.KeyIdentifiers = value; break;
                case nameof(Snake.TypicalSymptoms): snake.TypicalSymptoms = value; break;
                case nameof(Snake.Habitat): snake.Habitat = value; break;
                case nameof(Snake.DistributionNote): snake.DistributionNote = value; break;
                case nameof(Snake.Note): snake.Note = value; break;
                case nameof(Snake.IsActive):
                    if (bool.TryParse(value, out var isActive)) snake.IsActive = isActive;
                    break;
            }
        }

        #endregion
    }
}