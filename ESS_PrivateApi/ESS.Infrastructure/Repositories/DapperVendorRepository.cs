using Dapper;
using Dapper.Oracle;
using ESS.Domain.Abstractions;
using ESS.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace ESS.Infrastructure.Repositories
{
    public class DapperVendorRepository : IVendorRepository
    {
        private readonly string _connectionString;

        public DapperVendorRepository(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("OracleDb")!;
        }

        public async Task<Vendor?> GetByApiKeyAsync(string apiKey)
        {
            using var conn = new OracleConnection(_connectionString);

            var p = new OracleDynamicParameters();
            p.Add("p_flag", "1", OracleMappingType.Varchar2);
            p.Add("p_indata", $"{apiKey}", OracleMappingType.Varchar2);
            p.Add("p_as_outresult", dbType: OracleMappingType.RefCursor, direction: ParameterDirection.Output);
            p.Add("p_errorstat", dbType: OracleMappingType.Int32, direction: ParameterDirection.Output);
            p.Add("p_errormsg", dbType: OracleMappingType.Varchar2, direction: ParameterDirection.Output, size: 4000);


            var result = await conn.QueryAsync<Vendor>(
                "Proc_hrms_api_integration_zinghr",
                p,
                commandType: CommandType.StoredProcedure
            );

            return result.FirstOrDefault();
        }
    }
}