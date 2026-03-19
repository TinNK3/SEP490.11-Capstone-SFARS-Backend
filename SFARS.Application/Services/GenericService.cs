using MapsterMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Exceptions;
using SFARS.Application.Utils;
using SFARS.Application.Validations;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Application.Services
{
    public class GenericService<TEntity, TDto, TKey> : ReadOnlyService<TEntity, TDto, TKey>, IGenericService<TEntity, TDto, TKey>
        where TEntity : class
        where TDto : class
    {
        //    Summary:
        //        This class implements both
        public GenericService(
	        ISystemMessageService msgService,
	        IUnitOfWork unitOfWork, 
	        IMapper mapper,
	        ILogger logger) : base(msgService, unitOfWork, mapper, logger)
        {
            _msgService = msgService;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        /// <summary>
        /// CreateAsync
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public virtual async Task<IServiceResult> CreateAsync(TDto dto)
        {
            // Initiate service result
            var serviceResult = new ServiceResult();

            try
            {
                // Validate inputs using the generic validator
                var validationResult = await ValidatorExtensions.ValidateAsync(dto);
                // Check for valid validations
                if (validationResult != null && !validationResult.IsValid)
                {
                    // Convert ValidationResult to ValidationProblemsDetails.Errors
                    var errors = validationResult.ToProblemDetails().Errors;
                    throw new UnprocessableEntityException("Validation errors", errors);
                }

                // Process add new entity
                await _unitOfWork.Repository<TEntity, TKey>().AddAsync(_mapper.Map<TEntity>(dto));
                // Save to DB
                if (await _unitOfWork.SaveChangesAsync() > 0)
                {
                    serviceResult.ResultCode = ResultCodeConst.SYS_Success0001;
                    serviceResult.Message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001);
                }
                else
                {
                    serviceResult.ResultCode = ResultCodeConst.SYS_Fail0001;
                    serviceResult.Message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001);
                }
            }
            catch (UnprocessableEntityException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }

            return serviceResult;
        }

        /// <summary>
        /// UpdateAsync
        /// </summary>
        /// <param name="id"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        public virtual async Task<IServiceResult> UpdateAsync(TKey id, TDto dto)
        {
            // Initiate service result
            var serviceResult = new ServiceResult();

            try
            {
                // Validate inputs using the generic validator
                var validationResult = await ValidatorExtensions.ValidateAsync(dto);
                // Check for valid validations
                if (validationResult != null && !validationResult.IsValid)
                {
                    // Convert ValidationResult to ValidationProblemsDetails.Errors
                    var errors = validationResult.ToProblemDetails().Errors;
                    throw new UnprocessableEntityException("Invalid validations", errors);
                }

                // Retrieve the entity
                var existingEntity = await _unitOfWork.Repository<TEntity, TKey>().GetByIdAsync(id);
                if (existingEntity == null)
                {
                    var errMsg = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002);
                    return new ServiceResult(ResultCodeConst.SYS_Warning0002,
                        StringUtils.Format(errMsg, typeof(TEntity).ToString().ToLower()));
                }

                // Process add update entity
                // Map properties from dto to existingEntity
                _mapper.Map(dto, existingEntity);

                // Check if there are any differences between the original and the updated entity
                if (!_unitOfWork.Repository<TEntity, TKey>().HasChanges(existingEntity))
                {
                    serviceResult.ResultCode = ResultCodeConst.SYS_Success0003;
                    serviceResult.Message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003);
                    serviceResult.Data = true;
                    return serviceResult;
                }

                // Progress update when all require passed
                await _unitOfWork.Repository<TEntity, TKey>().UpdateAsync(existingEntity);

                // Save changes to DB
                var rowsAffected = await _unitOfWork.SaveChangesAsync();
                if (rowsAffected == 0)
                {
                    serviceResult.ResultCode = ResultCodeConst.SYS_Fail0003;
                    serviceResult.Message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0003);
                    serviceResult.Data = false;
                }

                // Mark as update success
                serviceResult.ResultCode = ResultCodeConst.SYS_Success0003;
                serviceResult.Message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003);
                serviceResult.Data = true;
            }
            catch (UnprocessableEntityException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }

            return serviceResult;
        }

        /// <summary>
        /// DeleteAsync
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public virtual async Task<IServiceResult> DeleteAsync(TKey id)
        {
            // Initiate service result
            var serviceResult = new ServiceResult();

            try
            {
                // EF Core 7+ ExecuteDeleteAsync - direct DELETE in database
                // Returns number of rows affected (0 if not found, 1+ if deleted)
                var rowsAffected = await _unitOfWork.Repository<TEntity, TKey>().DeleteAsync(id);
                
                if (rowsAffected == 0)
                {
                    // Entity not found
                    var errMsg = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002);
                    return new ServiceResult(ResultCodeConst.SYS_Warning0002,
                        StringUtils.Format(errMsg, nameof(TEntity).ToLower()));
                }
                
                // rowsAffected > 0 means delete success
                serviceResult.ResultCode = ResultCodeConst.SYS_Success0004;
                serviceResult.Message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0004);
                serviceResult.Data = true;
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException is SqlException sqlEx)
                {
                    switch (sqlEx.Number)
                    {
                        case 547: // Foreign key constraint violation
                            return new ServiceResult(ResultCodeConst.SYS_Fail0007,
                                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0007));
                    }
                }

                // Throw if other issues
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }

            return serviceResult;
        }
    }
}