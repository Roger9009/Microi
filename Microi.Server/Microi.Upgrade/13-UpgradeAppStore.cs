using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
namespace Microi.net
{
    /// <summary>
    /// 必要升级：应用商城
    /// </summary>
    public class UpgradeAppStore
    {
        /// <summary>
        /// 
        /// </summary>
        public static string Version = "5.5.5.0";
        
        /// <summary>
        /// 从嵌入资源读取文件内容
        /// </summary>
        private static string ReadEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fullResourceName = $"Microi.Upgrade.Resource.{resourceName}";
            
            using (Stream stream = assembly.GetManifestResourceStream(fullResourceName))
            {
                if (stream == null)
                {
                    throw new Exception($"嵌入资源未找到: {fullResourceName}");
                }
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public async Task<List<string>> Run(string osClient)
        {
            var msgs = new List<string>();
            
            #region 导入数据包V8
            var importV8 = ReadEmbeddedResource("import-package.js");
            var db = Microi.net.OsClient.GetClient(osClient).Db;
            var importApiIdObj = db.FromSql(
                    "SELECT Id FROM sys_apiengine WHERE ApiEngineKey = @ApiEngineKey")
                .AddInParameter("@ApiEngineKey", "import-microi-store-package")
                .ToScalar();
            var importApiId = importApiIdObj == null || Convert.IsDBNull(importApiIdObj)
                ? null
                : Convert.ToString(importApiIdObj);

            // 空库此时还没有完整的 diy_field 元数据，不能依赖 FormEngine 写入。
            // 使用底座 DbSession 做参数化幂等 DML，确保随后 ApiEngine 的固定查询可见。
            if (string.IsNullOrWhiteSpace(importApiId))
            {
                importApiId = Guid.NewGuid().ToString();
                db.FromSql(@"INSERT INTO sys_apiengine
(Id, CreateTime, UpdateTime, IsDeleted, ApiName, ApiEngineKey, ApiAddress, IsEnable, OsClient, ApiV8Code)
VALUES
(@Id, @Now, @Now, 0, @ApiName, @ApiEngineKey, @ApiAddress, 1, @OsClient, @ApiV8Code)")
                    .AddInParameter("@Id", importApiId)
                    .AddInParameter("@Now", DateTime.Now)
                    .AddInParameter("@ApiName", "[应用商城]导入Microi应用数据包")
                    .AddInParameter("@ApiEngineKey", "import-microi-store-package")
                    .AddInParameter("@ApiAddress", "/apiengine/import-microi-store-package")
                    .AddInParameter("@OsClient", osClient)
                    .AddInParameter("@ApiV8Code", importV8)
                    .ExecuteNonQuery();
            }
            else
            {
                db.FromSql(@"UPDATE sys_apiengine SET
UpdateTime = @Now, IsDeleted = 0, ApiName = @ApiName, ApiAddress = @ApiAddress,
IsEnable = 1, OsClient = @OsClient, ApiV8Code = @ApiV8Code
WHERE ApiEngineKey = @ApiEngineKey")
                    .AddInParameter("@Now", DateTime.Now)
                    .AddInParameter("@ApiName", "[应用商城]导入Microi应用数据包")
                    .AddInParameter("@ApiEngineKey", "import-microi-store-package")
                    .AddInParameter("@ApiAddress", "/apiengine/import-microi-store-package")
                    .AddInParameter("@OsClient", osClient)
                    .AddInParameter("@ApiV8Code", importV8)
                    .ExecuteNonQuery();
            }

            await MicroiEngine.CacheTenant.Cache(osClient).RemoveAsync($"Microi:{osClient}:FormData:sys_apiengine:import-microi-store-package");
            await MicroiEngine.CacheTenant.Cache(osClient).RemoveAsync($"Microi:{osClient}:FormData:sys_apiengine:{importApiId.ToLowerInvariant()}");
            await MicroiEngine.CacheTenant.Cache(osClient).RemoveAsync($"Microi:{osClient}:FormData:sys_apiengine:/apiengine/import-microi-store-package");

            var visibleCount = Convert.ToInt32(db.FromSql(
                    "SELECT COUNT(1) FROM sys_apiengine WHERE IsEnable = 1 AND ApiEngineKey = @ApiEngineKey AND IsDeleted <> 1")
                .AddInParameter("@ApiEngineKey", "import-microi-store-package")
                .ToScalar());
            if (visibleCount == 0)
            {
                msgs.Add("应用商城导入接口写入后校验失败：sys_apiengine 中无可用记录。");
                return msgs;
            }
            #endregion
            
            #region 模块引擎 数据包
            var dataModulePackage = ReadEmbeddedResource("app.microi.module-engine.json");
            //导入数据包
            var installModuleResult = await MicroiEngine.ApiEngine.RunAsync("import-microi-store-package", new
            {
                OsClient = osClient,
                Package = dataModulePackage
            });
            if(installModuleResult.Code != 1)
            {
                msgs.Add(installModuleResult.Msg);
            }
            #endregion

            #region 应用商城 数据包
            var appStorePackage = ReadEmbeddedResource("app.microi.store.json");
            //导入数据包
            var installAppStoreResult = await MicroiEngine.ApiEngine.RunAsync("import-microi-store-package", new
            {
                OsClient = osClient,
                Package = appStorePackage
            });
            if(installAppStoreResult.Code != 1)
            {
                msgs.Add(installAppStoreResult.Msg);
            }
            #endregion

            #region 表单引擎 数据包
            var formEnginePackage = ReadEmbeddedResource("app.microi.form-engine.json");
            //导入数据包
            var installFormEngineResult = await MicroiEngine.ApiEngine.RunAsync("import-microi-store-package", new
            {
                OsClient = osClient,
                Package = formEnginePackage
            });
            if(installFormEngineResult.Code != 1)
            {
                msgs.Add(installFormEngineResult.Msg);
            }
            #endregion

            #region 修正sys_menu的DiyTableId关联值
            var getStoreTableResult = await MicroiEngine.FormEngine.GetFormDataAsync("diy_table", new {
                OsClient = osClient,
                _Where = new List<object>()
                {
                    new List<object>() { "Name", "=", "sys_microistore" }
                }
            });
            if(getStoreTableResult.Code == 1){
                var getMenuResult = await MicroiEngine.FormEngine.GetFormDataAsync("sys_menu", new {
                    OsClient = osClient,
                    _Where = new List<object>()
                    {
                        new List<object>() { "ModuleEngineKey", "=", "sys_microistore" },
                    }
                });
                if(getMenuResult.Code == 1)
                {
                    var uptMenuResult = await MicroiEngine.FormEngine.UptFormDataAsync("sys_menu", new {
                        Id = (string)getMenuResult.Data.Id,
                        OsClient = osClient,
                        DiyTableId = (string)getStoreTableResult.Data.Id,
                        DiyTableName = (string)getStoreTableResult.Data.Name,
                    });
                    if(uptMenuResult.Code != 1)
                    {
                        msgs.Add(uptMenuResult.Msg);
                    }else
                    {
                        await MicroiEngine.CacheTenant.Cache(osClient).RemoveAsync($"Microi:{osClient}:FormData:sys_menu:{(string)getMenuResult.Data.Id}");
                        await MicroiEngine.CacheTenant.Cache(osClient).RemoveAsync($"Microi:{osClient}:FormData:sys_menu:sys_microistore");
                    }
                }
            }
            #endregion

            //更新缓存
            await MicroiEngine.CacheTenant.Cache(osClient).RemoveAsync($"Microi:{osClient}:FormData:diy_table:6cf254f1-edd0-4f04-96bc-c9ad08b5a2c");
            await MicroiEngine.CacheTenant.Cache(osClient).RemoveAsync($"Microi:{osClient}:FormData:diy_table_field_list:6cf254f1-edd0-4f04-96bc-c9ad08b5a2c");

            await MicroiEngine.CacheTenant.Cache(osClient).RemoveAsync($"Microi:{osClient}:FormData:diy_table:39bc4abe-98ee-46a7-b9d1-a7d649691193");
            await MicroiEngine.CacheTenant.Cache(osClient).RemoveAsync($"Microi:{osClient}:FormData:diy_table_field_list:39bc4abe-98ee-46a7-b9d1-a7d649691193");

            await MicroiEngine.CacheTenant.Cache(osClient).RemoveAsync($"Microi:{osClient}:FormData:diy_table:diy_table");
            await MicroiEngine.CacheTenant.Cache(osClient).RemoveAsync($"Microi:{osClient}:FormData:diy_table:diy_field");
            await MicroiEngine.CacheTenant.Cache(osClient).RemoveAsync($"Microi:{osClient}:FormData:diy_table:sys_microistore");
            await MicroiEngine.CacheTenant.Cache(osClient).RemoveAsync($"Microi:{osClient}:FormData:diy_table_field_list:sys_microistore");
            
            return msgs;
        }
    }
}

