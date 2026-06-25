-- =============================================================
-- Microi 业务底座 - 示例数据表（MySQL）
-- 表结构遵循平台 diy 表系统字段约定：
--   Id / CreateTime / UpdateTime / UserId(操作人) / UserName / IsDeleted / Sort
-- 业务字段中的 Status 为 int，与各模块的状态枚举对应。
--
-- 注意：本脚本仅创建物理表。若需在低代码平台的"表单引擎"中可视化管理，
--       请在平台中执行"加载非 diy 表"或通过 FormEngine.AddTable 注册元数据。
-- =============================================================

-- ----------------------------
-- ERP 销售订单：erp_sales_order
-- 状态(Status)：0=草稿 1=已提交 2=已审核 3=已完成 9=已作废
-- ----------------------------
DROP TABLE IF EXISTS `erp_sales_order`;
CREATE TABLE `erp_sales_order` (
  `Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT 'Id',
  `CreateTime` datetime NULL DEFAULT NULL COMMENT '创建时间',
  `UpdateTime` datetime NULL DEFAULT NULL COMMENT '修改时间',
  `UserId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '操作人Id',
  `UserName` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '操作人',
  `IsDeleted` bit(1) NULL DEFAULT b'0' COMMENT '是否删除',
  `Sort` int NULL DEFAULT 0 COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '备注',
  `BillNo` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '单据编号',
  `Status` int NULL DEFAULT 0 COMMENT '状态:0草稿/1已提交/2已审核/3已完成/9已作废',
  `CustomerId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '客户Id',
  `CustomerName` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '客户名称',
  `TotalAmount` decimal(18, 2) NULL DEFAULT NULL COMMENT '订单总金额',
  `OrderDate` datetime NULL DEFAULT NULL COMMENT '下单日期',
  `AuditorId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '审核人Id',
  `AuditTime` datetime NULL DEFAULT NULL COMMENT '审核时间',
  PRIMARY KEY (`Id`) USING BTREE,
  KEY `idx_erp_sales_order_billno` (`BillNo`) USING BTREE,
  KEY `idx_erp_sales_order_status` (`Status`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic COMMENT = 'ERP-销售订单';

-- ----------------------------
-- MES 生产工单：mes_work_order
-- 状态(Status)：0=已创建 1=已下达 2=生产中 3=已完工 4=已关闭 9=已取消
-- ----------------------------
DROP TABLE IF EXISTS `mes_work_order`;
CREATE TABLE `mes_work_order` (
  `Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT 'Id',
  `CreateTime` datetime NULL DEFAULT NULL COMMENT '创建时间',
  `UpdateTime` datetime NULL DEFAULT NULL COMMENT '修改时间',
  `UserId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '操作人Id',
  `UserName` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '操作人',
  `IsDeleted` bit(1) NULL DEFAULT b'0' COMMENT '是否删除',
  `Sort` int NULL DEFAULT 0 COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '备注',
  `BillNo` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '单据编号',
  `Status` int NULL DEFAULT 0 COMMENT '状态:0已创建/1已下达/2生产中/3已完工/4已关闭/9已取消',
  `SalesOrderId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '关联销售订单Id',
  `ProductId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '产品Id',
  `ProductName` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '产品名称',
  `PlanQty` decimal(18, 4) NULL DEFAULT NULL COMMENT '计划数量',
  `CompletedQty` decimal(18, 4) NULL DEFAULT 0 COMMENT '已完工数量',
  `PlanStartTime` datetime NULL DEFAULT NULL COMMENT '计划开工时间',
  `ActualStartTime` datetime NULL DEFAULT NULL COMMENT '实际开工时间',
  `ActualEndTime` datetime NULL DEFAULT NULL COMMENT '实际完工时间',
  PRIMARY KEY (`Id`) USING BTREE,
  KEY `idx_mes_work_order_billno` (`BillNo`) USING BTREE,
  KEY `idx_mes_work_order_status` (`Status`) USING BTREE,
  KEY `idx_mes_work_order_salesorder` (`SalesOrderId`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic COMMENT = 'MES-生产工单';
