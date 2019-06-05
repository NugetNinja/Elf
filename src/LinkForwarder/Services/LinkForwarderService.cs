﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Edi.Practice.RequestResponseModel;
using LinkForwarder.Controllers;
using LinkForwarder.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinkForwarder.Services
{
    public interface ILinkForwarderService
    {
        Task<Response<IReadOnlyList<RecentRequest>>> GetRecentRequests(int top);
        Task<Response<IReadOnlyList<Link>>> GetPagedLinks(int pageIndex, int pageSize, int take);
        Task<Response<bool>> IsLinkExists(string token);
    }

    public class LinkForwarderService : ILinkForwarderService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LinkForwarderService> _logger;

        private IDbConnection DbConnection => new SqlConnection(_configuration.GetConnectionString(Constants.DbName));

        public LinkForwarderService(IConfiguration configuration, ILogger<LinkForwarderService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<Response<IReadOnlyList<RecentRequest>>> GetRecentRequests(int top)
        {
            try
            {
                using (var conn = DbConnection)
                {
                    const string sql = @"SELECT TOP (@top)
                                         l.FwToken, l.OriginUrl, lt.IpAddress, lt.UserAgent, lt.RequestTimeUtc 
                                         FROM LinkTracking lt
                                         INNER JOIN Link l on lt.LinkId = l.Id ORDER BY lt.RequestTimeUtc DESC";
                    var list = await conn.QueryAsync<RecentRequest>(sql, new { top });
                    return new SuccessResponse<IReadOnlyList<RecentRequest>>(list.ToList());
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
                return new FailedResponse<IReadOnlyList<RecentRequest>>(e.Message);
            }
        }

        public async Task<Response<bool>> IsLinkExists(string token)
        {
            try
            {
                using (var conn = DbConnection)
                {
                    const string sql = @"SELECT TOP 1 1 FROM Link l
                                            WHERE l.FwToken = @token";
                    var exist = await conn.ExecuteScalarAsync<int>(sql, new { token }) == 1;
                    return new SuccessResponse<bool>(exist);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
                return new FailedResponse<bool>(e.Message);
            }
        }

        // TODO: Paging
        public async Task<Response<IReadOnlyList<Link>>> GetPagedLinks(int pageIndex, int pageSize, int take)
        {
            try
            {
                using (var conn = DbConnection)
                {
                    const string sql = @"SELECT 
                                         l.Id,
                                         l.OriginUrl,
                                         l.FwToken,
                                         l.Note,
                                         l.IsEnabled,
                                         l.UpdateTimeUtc
                                         FROM Link l 
                                         ORDER BY UpdateTimeUtc DESC";

                    var list = await conn.QueryAsync<Link>(sql);
                    return new SuccessResponse<IReadOnlyList<Link>>(list.ToList());
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
                return new FailedResponse<IReadOnlyList<Link>>(e.Message);
            }
        }
    }
}
