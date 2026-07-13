using Microsoft.Extensions.DependencyInjection;
using Quartz.AspNetCore;
using Quartz;
using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using Microsoft.Extensions.Configuration;
using Microi.net;
using System.Collections.Specialized;
using Quartz.Simpl;
using Microsoft.AspNetCore.Builder;

namespace Microi.net
{
    public static class MicroiJobExtension
    {
        /// <summary>
        /// 注入分布式任务调度插件，根据 dbType 自动匹配 Quartz 数据库提供程序。
        /// </summary>
        /// <param name="dbConn">数据库连接字符串</param>
        /// <param name="dbType">数据库类型（MySql/SqlServer/PostgreSql/Oracle/Sqlite3），默认 SqlServer</param>
        public static IServiceCollection AddMicroiJob(this IServiceCollection services, string dbConn, string dbType = "SqlServer")
        {
            try
            {
                services.AddQuartz(q =>
                {
                    q.UsePersistentStore(x =>
                    {
                        x.UseClustering();

                        // 根据 DbType 动态选择 Quartz 数据库提供程序
                        switch (dbType?.ToLower())
                        {
                            case "mysql":
                                x.UseMySql(dbConn);
                                break;
                            case "postgresql":
                                x.UsePostgres(dbConn);
                                break;
                            case "oracle":
                                // Oracle 需要 ODP.NET，使用 UseOracle 扩展
                                x.UseOracle(dbConn);
                                break;
                            case "sqlite":
                            case "sqlite3":
                                x.UseSQLite(dbConn);
                                break;
                            default: // SqlServer / SqlServer9
                                x.UseSqlServer(dbConn);
                                break;
                        }

                        x.UseNewtonsoftJsonSerializer();
                        x.SetProperty("quartz.jobStore.tablePrefix", "microi_job_");
                        x.SetProperty("quartz.jobStore.performSchemaValidation", "false");
                    });
                    q.AddJobListener<MicroiJobListener>();
                    q.UseDefaultThreadPool(tp =>
                    {
                        var maxConcurrency = Math.Max(4 * 10, Environment.ProcessorCount * 10);
                        tp.MaxConcurrency = maxConcurrency;
                        Console.WriteLine($"Microi：【成功】配置【分布式任务调度】插件线程最多[{maxConcurrency}]个！");
                    });
                });

                services.AddQuartzServer(options =>
                {
                    options.WaitForJobsToComplete = true;
                    options.StartDelay = TimeSpan.FromSeconds(10);
                });
                services.AddSingleton<IMicroiJob, MicroiQuartzScheduledTask>();
                Console.WriteLine($"Microi：【成功】注入【分布式任务调度】插件成功！（数据库类型：{dbType}）");
                return services;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Microi：【Error异常】注入【分布式任务调度】插件失败：" + ex.Message);
                return services;
            }
        }
        public static IApplicationBuilder UseMicroiJob(this IApplicationBuilder app)
        {
            try
            {
                var scheduledTask = app.ApplicationServices.GetRequiredService<IMicroiJob>();
                scheduledTask.SyncTaskTime();
                Console.WriteLine("Microi：【成功】【分布式任务调度】插件启动成功！");
                return app;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Microi：【Error异常】【分布式任务调度】插件启动失败：" + ex.Message);
                return app;
            }
        }
    }
}
