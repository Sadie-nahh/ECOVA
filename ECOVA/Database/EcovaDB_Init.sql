-- Tạo DB nếu chưa có
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ECOVA')
    CREATE DATABASE ECOVA;
GO
USE ECOVA;
GO

-- ==========================================
-- XÓA BẢNG CŨ (thứ tự ngược FK) nếu đã tồn tại
-- ==========================================
DROP TABLE IF EXISTS EmailQueue;
DROP TABLE IF EXISTS AuditLogs;
DROP TABLE IF EXISTS ResultHistory;
DROP TABLE IF EXISTS TestResults;
DROP TABLE IF EXISTS SamplingPlanItems;
DROP TABLE IF EXISTS Samples;
DROP TABLE IF EXISTS Orders;
DROP TABLE IF EXISTS RegulationLimits;
DROP TABLE IF EXISTS TestParameters;
DROP TABLE IF EXISTS Regulations;
DROP TABLE IF EXISTS CustomerFeedbacks;
DROP TABLE IF EXISTS Contracts;
DROP TABLE IF EXISTS Users;
DROP TABLE IF EXISTS Customers;
DROP TABLE IF EXISTS Roles;
GO

-- ==========================================
-- 1. PHÂN HỆ QUẢN TRỊ & BẢO MẬT (AUTH_SYSTEM)
-- ==========================================

CREATE TABLE Roles (
    RoleID      VARCHAR(50)     PRIMARY KEY,
    RoleCode    VARCHAR(20)     UNIQUE NOT NULL,
    RoleName    NVARCHAR(100)   NOT NULL,
    Description NVARCHAR(255)   NULL
);

CREATE TABLE Users (
    UserID             VARCHAR(50)    PRIMARY KEY,
    EmployeeCode       VARCHAR(20)    NOT NULL,
    Username           VARCHAR(50)    UNIQUE NOT NULL,
    PasswordHash       VARCHAR(255)   NOT NULL,
    FullName           NVARCHAR(100)  NOT NULL,
    Email              VARCHAR(100)   UNIQUE NOT NULL,
    Phone              VARCHAR(20)    NULL,
    DateOfBirth        DATETIME       NULL,
    Department         NVARCHAR(100)  NULL,
    Address            NVARCHAR(255)  NULL,
    AvatarData         VARBINARY(MAX) NULL,
    FaceIDData         VARBINARY(MAX) NULL,
    IsFaceIDRegistered BIT            DEFAULT 0 NOT NULL,
    RoleID             VARCHAR(50)    FOREIGN KEY REFERENCES Roles(RoleID),
    IsActive           BIT            DEFAULT 1 NOT NULL,
    CreatedDate        DATETIME       DEFAULT GETDATE(),
    UpdatedAt          DATETIME2      NULL,
    CONSTRAINT CK_Users_IsActive           CHECK (IsActive IN (0, 1)),
    CONSTRAINT CK_Users_IsFaceIDRegistered CHECK (IsFaceIDRegistered IN (0, 1))
);

-- ==========================================
-- 2. PHÂN HỆ KINH DOANH & KHÁCH HÀNG (SALES_MODULE)
-- ==========================================

CREATE TABLE Customers (
    CustomerID     VARCHAR(50)    PRIMARY KEY,
    TaxCode        VARCHAR(20)    UNIQUE NOT NULL,
    CompanyName    NVARCHAR(200)  NOT NULL,
    Address        NVARCHAR(255)  NULL,
    Representative NVARCHAR(100)  NULL,
    ContactEmail   VARCHAR(100)   NOT NULL,
    PhoneNumber    VARCHAR(20)    NULL
);

CREATE TABLE CustomerFeedbacks (
    FeedbackID         VARCHAR(50) PRIMARY KEY,
    CustomerID         VARCHAR(50) FOREIGN KEY REFERENCES Customers(CustomerID),
    ResponseSpeed      INT         NULL,
    ResponseTime       INT         NULL,
    PreviousViolations INT         DEFAULT 0,
    Frequency          INT         DEFAULT 0,
    CreatedDate        DATETIME    DEFAULT GETDATE(),
    CONSTRAINT CK_CustomerFeedbacks_Score
        CHECK (ResponseSpeed IS NULL OR (ResponseSpeed >= 1 AND ResponseSpeed <= 10))
);

CREATE TABLE Contracts (
    ContractID         VARCHAR(50)     PRIMARY KEY,
    CustomerID         VARCHAR(50)     FOREIGN KEY REFERENCES Customers(CustomerID),
    SignedDate         DATETIME        NOT NULL,
    ValidFrom          DATETIME        NOT NULL,
    ValidTo            DATETIME        NOT NULL,
    ContractFilePath   NVARCHAR(500)   NULL,
    Status             INT             DEFAULT 0 NOT NULL,   -- 0=Active,1=Suspended,2=Expiring,3=Expired,4=Cancelled
    CreatedBy          VARCHAR(50)     FOREIGN KEY REFERENCES Users(UserID),
    TotalContractValue DECIMAL(18, 2)  NULL,
    IndustryType       NVARCHAR(50)    NULL,
    RenewalLabel       INT             NULL,                 -- 0=Không gia hạn, 1=Gia hạn
    UpdatedAt          DATETIME2       NULL,
    CONSTRAINT CK_Contracts_Status      CHECK (Status IN (0, 1, 2, 3, 4)),
    CONSTRAINT CK_Contracts_ValidDates  CHECK (ValidTo > ValidFrom),
    CONSTRAINT CK_Contracts_RenewalLabel CHECK (RenewalLabel IS NULL OR RenewalLabel IN (0, 1))
);

CREATE TABLE Orders (
    OrderID         VARCHAR(50)   PRIMARY KEY,
    ContractID      VARCHAR(50)   FOREIGN KEY REFERENCES Contracts(ContractID),
    OrderName       NVARCHAR(200) NULL,
    EnvironmentType NVARCHAR(50)  NULL,                      -- N'Không khí', N'Nước thải', N'Đất'
    OrderDate       DATETIME      DEFAULT GETDATE(),
    Deadline        DATETIME      NULL,
    FinalReportPath NVARCHAR(500) NULL,
    IsApproved      INT           DEFAULT 0 NOT NULL,        -- 0=Chờ duyệt, 1=Đã duyệt
    Status          INT           DEFAULT 0 NOT NULL,        -- 0=Mới, 1=Đang thực hiện, 2=Hoàn thành
    CreatedBy       VARCHAR(50)   NULL,
    UpdatedAt       DATETIME2     NULL,
    CONSTRAINT CK_Orders_Status     CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_Orders_IsApproved CHECK (IsApproved IN (0, 1))
);

-- ==========================================
-- 3. PHÂN HỆ MASTER DATA (CORE_LOGIC)
-- ==========================================

CREATE TABLE Regulations (
    RegulationID    VARCHAR(50)   PRIMARY KEY,
    Code            NVARCHAR(50)  NOT NULL,
    Name            NVARCHAR(200) NULL,
    EnvironmentType NVARCHAR(50)  NULL
);

CREATE TABLE TestParameters (
    ParamID    VARCHAR(50)    PRIMARY KEY,
    ParamName  NVARCHAR(100)  NOT NULL,
    Unit       NVARCHAR(20)   NULL,
    TestMethod NVARCHAR(100)  NULL,
    IsField    INT            NOT NULL,
    Price      DECIMAL(18, 2) DEFAULT 0
);

CREATE TABLE RegulationLimits (
    LimitID      VARCHAR(50) PRIMARY KEY,
    RegulationID VARCHAR(50) FOREIGN KEY REFERENCES Regulations(RegulationID),
    ParamID      VARCHAR(50) FOREIGN KEY REFERENCES TestParameters(ParamID),
    MinValue     FLOAT       NULL,
    MaxValue     FLOAT       NULL
);

-- ==========================================
-- 4. PHÂN HỆ VẬN HÀNH (OPERATIONS)
-- ==========================================

-- Bảng kế hoạch lấy mẫu: mỗi dòng = 1 thông số cần đo trong 1 khu vực
CREATE TABLE SamplingPlanItems (
    PlanItemID   VARCHAR(50)    PRIMARY KEY,
    OrderID      VARCHAR(50)    FOREIGN KEY REFERENCES Orders(OrderID),
    ParamID      VARCHAR(50)    FOREIGN KEY REFERENCES TestParameters(ParamID),
    RegulationID VARCHAR(50)    NULL,
    Department   NVARCHAR(50)   NOT NULL,  -- N'Hiện trường' hoặc N'Thí nghiệm'
    QcvnLimit    NVARCHAR(100)  NULL,      -- Giá trị ngưỡng QCVN, vd: '≤ 350'
    CONSTRAINT FK_SamplingPlanItems_Regulation FOREIGN KEY (RegulationID) REFERENCES Regulations(RegulationID)
);

CREATE TABLE Samples (
    SampleID         VARCHAR(50)   PRIMARY KEY,
    OrderID          VARCHAR(50)   FOREIGN KEY REFERENCES Orders(OrderID),
    RegulationID     VARCHAR(50)   FOREIGN KEY REFERENCES Regulations(RegulationID),
    Barcode          VARCHAR(50)   UNIQUE NOT NULL,
    SamplingLocation NVARCHAR(200) NULL,
    SamplingTime     DATETIME      NULL,
    FieldTemperature FLOAT         NULL,
    FieldHumidity    FLOAT         NULL,
    WeatherCondition NVARCHAR(200) NULL,
    FieldImage       NVARCHAR(500) NULL,
    IsWarning        BIT           DEFAULT 0,
    SamplerID        VARCHAR(50)   FOREIGN KEY REFERENCES Users(UserID),
    Status           INT           DEFAULT 0 NOT NULL,       -- 0=Mới lấy, 1=Đang phân tích, 2=Đã hủy, 3=Hoàn thành
    UpdatedAt        DATETIME2     NULL,
    CONSTRAINT CK_Samples_Status CHECK (Status IN (0, 1, 2, 3))
);

CREATE TABLE TestResults (
    ResultID    VARCHAR(50) PRIMARY KEY,
    SampleID    VARCHAR(50) FOREIGN KEY REFERENCES Samples(SampleID),
    ParamID     VARCHAR(50) FOREIGN KEY REFERENCES TestParameters(ParamID),
    ResultValue FLOAT       NOT NULL,
    IsWarning   BIT         DEFAULT 0,
    TesterID    VARCHAR(50) FOREIGN KEY REFERENCES Users(UserID),
    EnteredAt   DATETIME    DEFAULT GETDATE()
);

-- ==========================================
-- 5. PHÂN HỆ TIỆN ÍCH (UTILITIES)
-- ==========================================

CREATE TABLE ResultHistory (
    HistoryID VARCHAR(50) PRIMARY KEY,
    ResultID  VARCHAR(50) FOREIGN KEY REFERENCES TestResults(ResultID),
    OldValue  FLOAT       NULL,
    NewValue  FLOAT       NULL,
    ChangedBy VARCHAR(50) FOREIGN KEY REFERENCES Users(UserID),
    ChangedAt DATETIME    DEFAULT GETDATE()
);

CREATE TABLE EmailQueue (
    EmailID        VARCHAR(50)    PRIMARY KEY,
    Recipient      VARCHAR(100)   NULL,
    Subject        NVARCHAR(200)  NULL,
    Body           NVARCHAR(MAX)  NULL,
    AttachmentPath NVARCHAR(500)  NULL,
    Status         INT            DEFAULT 0 NOT NULL,        -- 0=Pending, 1=Sent, 2=Failed
    CreatedTime    DATETIME       DEFAULT GETDATE(),
    Type           NVARCHAR(50)   NULL,
    CONSTRAINT CK_EmailQueue_Status CHECK (Status IN (0, 1, 2))
);

-- Bảng audit-log toàn hệ thống: ghi nhận login, CRUD hợp đồng, nhân viên, kết quả
CREATE TABLE AuditLogs (
    LogID      VARCHAR(50)     PRIMARY KEY DEFAULT NEWID(),
    UserID     VARCHAR(50)     NULL FOREIGN KEY REFERENCES Users(UserID),
    Action     NVARCHAR(100)   NOT NULL,   -- N'LOGIN', N'LOGOUT', N'CREATE_CONTRACT', N'DELETE_EMPLOYEE'
    EntityType NVARCHAR(50)    NULL,       -- N'Contract', N'Employee', N'User', N'Order'
    EntityID   VARCHAR(50)     NULL,       -- ID của bản ghi bị tác động
    Detail     NVARCHAR(500)   NULL,       -- Thông tin chi tiết thêm
    LoggedAt   DATETIME2       DEFAULT SYSDATETIME()
);
GO

-- ==========================================
-- 6. PERFORMANCE INDEXES
-- ==========================================

-- Users
CREATE NONCLUSTERED INDEX IX_Users_Email    ON Users(Email)    WHERE Email IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_Users_IsActive ON Users(IsActive, Username);

-- Contracts
CREATE NONCLUSTERED INDEX IX_Contracts_Status
    ON Contracts(Status) INCLUDE (CustomerID, ValidFrom, ValidTo, TotalContractValue);
CREATE NONCLUSTERED INDEX IX_Contracts_CustomerID
    ON Contracts(CustomerID) INCLUDE (Status, ValidFrom, ValidTo);
CREATE NONCLUSTERED INDEX IX_Contracts_ValidTo
    ON Contracts(ValidTo) WHERE Status IN (0, 2);

-- Orders
CREATE NONCLUSTERED INDEX IX_Orders_ContractID
    ON Orders(ContractID) INCLUDE (Status, IsApproved, OrderDate);

-- Samples
CREATE NONCLUSTERED INDEX IX_Samples_OrderID
    ON Samples(OrderID) INCLUDE (Status, IsWarning, Barcode);
CREATE UNIQUE NONCLUSTERED INDEX IX_Samples_Barcode ON Samples(Barcode);

-- SamplingPlanItems
CREATE NONCLUSTERED INDEX IX_SamplingPlanItems_OrderID
    ON SamplingPlanItems(OrderID) INCLUDE (ParamID, Department, QcvnLimit);

-- TestResults
CREATE NONCLUSTERED INDEX IX_TestResults_SampleID
    ON TestResults(SampleID) INCLUDE (ParamID, ResultValue, IsWarning);

-- RegulationLimits (composite — dùng khi check kết quả vs quy chuẩn)
CREATE NONCLUSTERED INDEX IX_RegulationLimits_Composite
    ON RegulationLimits(RegulationID, ParamID) INCLUDE (MinValue, MaxValue);

-- EmailQueue (background job chỉ query Status=0)
CREATE NONCLUSTERED INDEX IX_EmailQueue_Pending ON EmailQueue(Status) WHERE Status = 0;

-- AuditLogs (query theo user và theo thời gian)
CREATE NONCLUSTERED INDEX IX_AuditLogs_UserID  ON AuditLogs(UserID, LoggedAt DESC);
CREATE NONCLUSTERED INDEX IX_AuditLogs_LoggedAt ON AuditLogs(LoggedAt DESC);
GO

-- ==========================================
-- 7. STORED PROCEDURES — AUDIT LOG
-- ==========================================

CREATE OR ALTER PROCEDURE sp_AddAuditLog
    @UserID     VARCHAR(50),
    @Action     NVARCHAR(100),
    @EntityType NVARCHAR(50)  = NULL,
    @EntityID   VARCHAR(50)   = NULL,
    @Detail     NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO AuditLogs (LogID, UserID, Action, EntityType, EntityID, Detail)
    VALUES (NEWID(), @UserID, @Action, @EntityType, @EntityID, @Detail);
END
GO

CREATE OR ALTER PROCEDURE sp_GetAuditLogs
    @UserID     VARCHAR(50)   = NULL,   -- NULL = lấy tất cả
    @TopN       INT           = 100
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@TopN)
        al.LogID, al.Action, al.EntityType, al.EntityID,
        al.Detail, al.LoggedAt,
        u.FullName AS UserName, u.Department
    FROM AuditLogs al
    LEFT JOIN Users u ON u.UserID = al.UserID
    WHERE (@UserID IS NULL OR al.UserID = @UserID)
    ORDER BY al.LoggedAt DESC;
END
GO
