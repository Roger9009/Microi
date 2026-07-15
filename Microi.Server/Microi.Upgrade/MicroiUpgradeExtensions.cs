using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace Microi.net
{
    public static class MicroiUpgradeExtensions
    {
        public static IServiceCollection AddMicroiUpgrade(this IServiceCollection services)
        {
            try
            {
                services.AddSingleton<IMicroiUpgrade, MicroiUpgrade>();
                Console.WriteLine("Microi：【成功】注入【服务器端自动升级】插件成功！");
                return services;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Microi：【Error异常】注入【服务器端自动升级】插件失败：" + ex.Message);
                return services;
            }
        }
        public static IApplicationBuilder UseMicroiUpgrade(this IApplicationBuilder app)
        {
            try
            {
                var scheduledTask = app.ApplicationServices.GetRequiredService<IMicroiUpgrade>();
                var _formEngine = app.ApplicationServices.GetRequiredService<IFormEngine>();
                if (scheduledTask != null)
                {
                    // 核心 Schema 必须同步完成：Program.cs 会在 UseMicroiUpgrade() 返回后立即
                    // EnsureHydrated，若仍放在 Task.Run 中会与 sys_osclients 挂载产生竞态。
                    foreach (var clientModelItem in OsClient.ClientList)
                    {
                        var readyClient = OsClient.GetClient(clientModelItem.Key);
                        var dbConn = readyClient.OsClientModel?["DbConn"]?.ToString();
                        if (string.IsNullOrWhiteSpace(dbConn)) continue;
                        try
                        {
                            var initialized = CoreTableInitializer.EnsureTables(readyClient);
                            var dbTypeReady = readyClient.OsClientModel?["DbType"]?.ToString() ?? "MySql";
                            if (initialized)
                            {
                                Console.WriteLine($"Microi：【✅】核心平台表已就绪（{dbTypeReady}，底座 DDL）。");
                            }
                            else
                            {
                                Console.WriteLine($"Microi：【⚠️】核心平台表未完成初始化（{dbTypeReady}，DbSession 不可用）。");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Microi：【⚠️】核心表初始化：{ex.Message.Split('\n')[0]}");
                        }
                    }

                    #region 平台自动升级
                    Task.Run(async () =>
                    {
                        foreach (var clientModelItem in OsClient.ClientList)
                        {
                            var dbConn = clientModelItem.Value.OsClientModel?["DbConn"]?.ToString();
                            if (string.IsNullOrWhiteSpace(dbConn)) { Console.WriteLine($"Microi：【⚠️】跳过无 DB 租户 [{clientModelItem.Value.OsClient}]"); continue; }

                            // diy_lang / diy_license 物理表已由 CoreTableInitializer（底座 AddDiyTable/AddColumn）创建；
                            // 多语言数据与接口种子由后续 Upgrade() 脚本写入。

                            try
                            {
                                //获取当前数据库版本号
                                var versionResult = await _formEngine.GetFormDataAsync<SysConfig>(new
                                {
                                    FormEngineKey = "sys_config",
                                    _Where = new List<DiyWhere>() {
                                    new DiyWhere() {
                                        Name = "IsEnable",
                                        Value = "1",
                                        Type = "="
                                    }
                                },
                                    OsClient = clientModelItem.Value.OsClient
                                });
                                var currentVersion = "";
                                if (versionResult.Code == 1)
                                {
                                    currentVersion = versionResult.Data.ServerVersion ?? "";
                                }
                                try
                                {
                                    // var sqlResult = await new MicroiUpgrade().Upgrade(currentVersion, clientModelItem.Value);
                                    await scheduledTask.Upgrade(currentVersion, clientModelItem.Value);

                                    // if (sqlResult.Code == 1)
                                    // {
                                    //     foreach (var upgdareItem in sqlResult.Data)
                                    //     {
                                    //         try
                                    //         {
                                    //             var count = clientModelItem.Value.Db.FromSql(upgdareItem.Sql).ExecuteNonQuery();
                                    //         }
                                    //         catch (Exception ex)
                                    //         {
                                    //             Console.WriteLine($"Microi：平台自动升级升级执行sql失败：Sql：{upgdareItem.Sql}。{OsClientDefault.OsClient}-{OsClient.OsClientType}-{OsClient.OsClientNetwork}-ClientList[{ClientList.Count}]。-->{ex.Message}");
                                    //         }
                                    //     }
                                    // }
                                    // else
                                    // {
                                    //     Console.WriteLine($"Microi：平台自动升级升级获取sql失败：{OsClientDefault.OsClient}-{OsClient.OsClientType}-{OsClient.OsClientNetwork}-ClientList[{ClientList.Count}]。-->{sqlResult.Msg}");
                                    // }

                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Microi：【Error异常】【{clientModelItem.Value.OsClient}】平台自动升级出现异常：{ex.Message}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Microi：【Error异常】【{clientModelItem.Value.OsClient}】平台自动升级出现异常：{ex.Message}");
                            }
                            // if (DiyMessage.Msg.Count == 0)
                            {
                                #region 加载多语言
                                try
                                {
                                    // var langList = currentClientModel.Db.FromSql("select * from diy_lang").ToList<DiyLang>();
                                    var langList = clientModelItem.Value.Db.FromSql("select * from diy_lang").ToList<dynamic>();
                                    // var langs = new List<string>(){
                                    //     "zh-cn", "zh", "cn", "en", "zh-tw"
                                    // };
                                    var langLevel2 = new Dictionary<string, JObject>();
                                    foreach (var item in langList)
                                    {
                                        JObject itemObj = JObject.FromObject(item);
                                        var key = itemObj["Key"]?.ToString();
                                        langLevel2.Add(key, itemObj);
                                    }
                                    if (DiyMessage.Msg.ContainsKey(clientModelItem.Value.OsClient))
                                    {
                                        DiyMessage.Msg[clientModelItem.Value.OsClient] = langLevel2;
                                    }
                                    else
                                    {
                                        DiyMessage.Msg.Add(clientModelItem.Value.OsClient, langLevel2);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Microi：【Error异常】【{clientModelItem.Value.OsClient}】加载多语言出现异常：{ex.Message}");
                                }
                                #endregion
                            }
                        }
                    });
                    #endregion
                }
                return app;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Microi：【Error异常】服务器端自动升级失败：" + ex.Message);
                return app;
            }
        }
    }
}

