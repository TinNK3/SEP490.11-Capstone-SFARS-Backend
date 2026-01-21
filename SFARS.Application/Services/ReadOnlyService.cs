using MapsterMapper;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SFARS.Application.Services
{
    public class ReadOnlyService<TEntity, TDto, TKey> : IReadOnlyService<TEntity, TDto, TKey>
        where TEntity : class
        where TDto : class
    {
        protected IUnitOfWork _unitOfWork;
        protected IMapper _mapper;
        protected ILogger _logger;
        protected ISystemMessageService _msgService;

        public ReadOnlyService(
            ISystemMessageService msgService,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger logger)
        {
            _logger = logger;
            _msgService = msgService;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public virtual async Task<IServiceResult> GetAllAsync(bool tracked = true)
        {
            try
            {
                var entities = await _unitOfWork.Repository<TEntity, TKey>().GetAllAsync();

                if (!entities.Any())
                {
                    return new ServiceResult(ResultCodeConst.SYS_Warning0004,
                        string.Empty,
                        _mapper.Map<IEnumerable<TDto>>(entities));
                }

                return new ServiceResult(ResultCodeConst.SYS_Success0002,
                    string.Empty,
                    _mapper.Map<IEnumerable<TDto>>(entities));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception("Error invoke when progress get all data");
            }
        }

        public virtual async Task<IServiceResult> GetByIdAsync(TKey id)
        {
            try
            {
                var entity = await _unitOfWork.Repository<TEntity, TKey>().GetByIdAsync(id);

                if (entity == null)
                {
                    return new ServiceResult(ResultCodeConst.SYS_Warning0004,
                        string.Empty);
                }

                return new ServiceResult(ResultCodeConst.SYS_Success0002,
                    string.Empty,
                    _mapper.Map<TDto>(entity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception("Error invoke when progress get data");
            }
        }

        public virtual async Task<IServiceResult> GetWithSpecAsync(ISpecification<TEntity> specification, bool tracked = true)
        {
            try
            {
                var entity = await _unitOfWork.Repository<TEntity, TKey>().GetWithSpecAsync(specification, tracked);

                if (entity == null)
                {
                    return new ServiceResult(ResultCodeConst.SYS_Warning0004,
                        string.Empty);
                }

                return new ServiceResult(ResultCodeConst.SYS_Success0002,
                    string.Empty,
                    _mapper.Map<TDto>(entity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception("Error invoke when progress get data");
            }
        }

        public virtual async Task<IServiceResult> GetAllWithSpecAsync(ISpecification<TEntity> specification, bool tracked = true)
        {
            try
            {
                var entities = await _unitOfWork.Repository<TEntity, TKey>().GetAllWithSpecAsync(specification, tracked);

                if (!entities.Any())
                {
                    return new ServiceResult(ResultCodeConst.SYS_Warning0004,
                        string.Empty,
                        _mapper.Map<IEnumerable<TDto>>(entities));
                }

                return new ServiceResult(ResultCodeConst.SYS_Success0002,
                    string.Empty,
                    _mapper.Map<IEnumerable<TDto>>(entities));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception($"Error invoke when progress get all data: {ex.Message} {ex.InnerException?.Message}");
            }
        }

        public virtual async Task<IServiceResult> GetWithSpecAndSelectorAsync<TResult>(
            ISpecification<TEntity> specification,
            Expression<Func<TEntity, TResult>> selector,
            bool tracked = true)
        {
            try
            {
                var tResult = await _unitOfWork.Repository<TEntity, TKey>().GetWithSpecAndSelectorAsync(
                    specification, selector, tracked);

                if (tResult == null)
                {
                    return new ServiceResult(ResultCodeConst.SYS_Warning0004,
                        string.Empty);
                }

                return new ServiceResult(ResultCodeConst.SYS_Success0002,
                    string.Empty, tResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception("Error invoke when progress get data by selector");
            }
        }

        public virtual async Task<IServiceResult> GetAllWithSpecAndSelectorAsync<TResult>(
            ISpecification<TEntity> specification,
            Expression<Func<TEntity, TResult>> selector,
            bool tracked = true)
        {
            try
            {
                var tResults = await _unitOfWork.Repository<TEntity, TKey>()
                    .GetAllWithSpecAndSelectorAsync(specification, selector, tracked);

                if (!tResults.Any())
                {
                    return new ServiceResult(ResultCodeConst.SYS_Warning0004,
                        string.Empty, tResults);
                }

                return new ServiceResult(ResultCodeConst.SYS_Success0002,
                    string.Empty, tResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception("Error invoke when progress get all data by selector");
            }
        }

        public virtual async Task<IServiceResult> AnyAsync(Expression<Func<TEntity, bool>> predicate)
        {
            try
            {
                var hasAny = await _unitOfWork.Repository<TEntity, TKey>().AnyAsync(predicate);

                if (!hasAny)
                {
                    return new ServiceResult(ResultCodeConst.SYS_Warning0004,
                        string.Empty, false);
                }

                return new ServiceResult(ResultCodeConst.SYS_Success0002,
                    string.Empty, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception("Error invoke when progress check total data");
            }
        }

        public virtual async Task<IServiceResult> AnyAsync(ISpecification<TEntity> specification)
        {
            try
            {
                var hasAny = await _unitOfWork.Repository<TEntity, TKey>().AnyAsync(specification);

                if (!hasAny)
                {
                    return new ServiceResult(ResultCodeConst.SYS_Warning0004,
                        string.Empty, false);
                }

                return new ServiceResult(ResultCodeConst.SYS_Success0002,
                    string.Empty, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception("Error invoke when progress check total data");
            }
        }

        public virtual async Task<IServiceResult> SumAsync(Expression<Func<TEntity, int>> predicate)
        {
            try
            {
                var countRes = await _unitOfWork.Repository<TEntity, TKey>().SumAsync(predicate);
                return new ServiceResult(ResultCodeConst.SYS_Success0002,
                    string.Empty, countRes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception("Error invoke when progress sum data");
            }
        }

        public virtual async Task<IServiceResult> SumWithSpecAsync(
            ISpecification<TEntity> specification,
            Expression<Func<TEntity, int>> predicate)
        {
            try
            {
                var countRes = await _unitOfWork.Repository<TEntity, TKey>().SumWithSpecAsync(specification, predicate);
                return new ServiceResult(ResultCodeConst.SYS_Success0002,
                    string.Empty, countRes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception("Error invoke when progress sum data");
            }
        }

        public virtual async Task<IServiceResult> CountAsync(ISpecification<TEntity> specification)
        {
            try
            {
                var totalEntity = await _unitOfWork.Repository<TEntity, TKey>().CountAsync();
                return new ServiceResult(ResultCodeConst.SYS_Success0002,
                    string.Empty, totalEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception("Error invoke when progress count data");
            }
        }

        public async Task<IServiceResult> CountAsync()
        {
            try
            {
                var totalEntity = await _unitOfWork.Repository<TEntity, TKey>().CountAsync();
                return new ServiceResult(ResultCodeConst.SYS_Success0002,
                    string.Empty, totalEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception("Error invoke when progress count data");
            }
        }
    }
}
