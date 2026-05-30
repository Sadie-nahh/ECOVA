USE ECOVA;
GO
SET NOCOUNT ON;
GO

-- ============================================================
-- XÓA CÁC OBJECT CŨ NẾU TỒN TẠI (idempotent)
-- ============================================================
PRINT 'Dropping existing programmability objects...';
GO

-- Triggers
IF OBJECT_ID('trg_TestResults_AutoWarning',  'TR') IS NOT NULL DROP TRIGGER trg_TestResults_AutoWarning;
IF OBJECT_ID('trg_TestResults_AuditHistory', 'TR') IS NOT NULL DROP TRIGGER trg_TestResults_AuditHistory;
IF OBJECT_ID('trg_Users_UpdatedAt',          'TR') IS NOT NULL DROP TRIGGER trg_Users_UpdatedAt;
IF OBJECT_ID('trg_Contracts_UpdatedAt',      'TR') IS NOT NULL DROP TRIGGER trg_Contracts_UpdatedAt;
IF OBJECT_ID('trg_Orders_UpdatedAt',         'TR') IS NOT NULL DROP TRIGGER trg_Orders_UpdatedAt;
IF OBJECT_ID('trg_Samples_UpdatedAt',        'TR') IS NOT NULL DROP TRIGGER trg_Samples_UpdatedAt;
GO

-- Functions
IF OBJECT_ID('fn_IsResultExceedingLimit',      'FN') IS NOT NULL DROP FUNCTION fn_IsResultExceedingLimit;
IF OBJECT_ID('fn_GetContractStatus',           'FN') IS NOT NULL DROP FUNCTION fn_GetContractStatus;
IF OBJECT_ID('fn_GetOrderCompletionPercent',   'FN') IS NOT NULL DROP FUNCTION fn_GetOrderCompletionPercent;
IF OBJECT_ID('fn_FormatContractID',            'FN') IS NOT NULL DROP FUNCTION fn_FormatContractID;
IF OBJECT_ID('fn_GetParametersByRegulation',   'IF') IS NOT NULL DROP FUNCTION fn_GetParametersByRegulation;
IF OBJECT_ID('fn_GetTestResultsWithLimits',    'IF') IS NOT NULL DROP FUNCTION fn_GetTestResultsWithLimits;
GO

-- Stored Procedures
IF OBJECT_ID('sp_GetDashboardStats',        'P') IS NOT NULL DROP PROCEDURE sp_GetDashboardStats;
IF OBJECT_ID('sp_GetContractCards',         'P') IS NOT NULL DROP PROCEDURE sp_GetContractCards;
IF OBJECT_ID('sp_SearchContracts',          'P') IS NOT NULL DROP PROCEDURE sp_SearchContracts;
IF OBJECT_ID('sp_GetExpiringContracts',     'P') IS NOT NULL DROP PROCEDURE sp_GetExpiringContracts;
IF OBJECT_ID('sp_UpdateContractStatuses',   'P') IS NOT NULL DROP PROCEDURE sp_UpdateContractStatuses;
IF OBJECT_ID('sp_GetOrdersByContract',      'P') IS NOT NULL DROP PROCEDURE sp_GetOrdersByContract;
IF OBJECT_ID('sp_GetTestResultsByOrder',    'P') IS NOT NULL DROP PROCEDURE sp_GetTestResultsByOrder;
IF OBJECT_ID('sp_SaveTestResult',           'P') IS NOT NULL DROP PROCEDURE sp_SaveTestResult;
IF OBJECT_ID('sp_GetSamplingPlanByOrder',   'P') IS NOT NULL DROP PROCEDURE sp_GetSamplingPlanByOrder;
IF OBJECT_ID('sp_GetWarningResults',        'P') IS NOT NULL DROP PROCEDURE sp_GetWarningResults;
IF OBJECT_ID('sp_GetRevenueByQuarter',      'P') IS NOT NULL DROP PROCEDURE sp_GetRevenueByQuarter;
IF OBJECT_ID('sp_GetEmployeeList',          'P') IS NOT NULL DROP PROCEDURE sp_GetEmployeeList;
IF OBJECT_ID('sp_AuthenticateUser',         'P') IS NOT NULL DROP PROCEDURE sp_AuthenticateUser;
IF OBJECT_ID('sp_GetContractReport',        'P') IS NOT NULL DROP PROCEDURE sp_GetContractReport;
IF OBJECT_ID('sp_GenerateEmployeeCode',     'P') IS NOT NULL DROP PROCEDURE sp_GenerateEmployeeCode;
IF OBJECT_ID('sp_UpsertEmployee',           'P') IS NOT NULL DROP PROCEDURE sp_UpsertEmployee;
IF OBJECT_ID('sp_DeleteSamplingArea',       'P') IS NOT NULL DROP PROCEDURE sp_DeleteSamplingArea;
IF OBJECT_ID('sp_SaveSamplingPlan',         'P') IS NOT NULL DROP PROCEDURE sp_SaveSamplingPlan;
IF OBJECT_ID('sp_RegisterFaceID',           'P') IS NOT NULL DROP PROCEDURE sp_RegisterFaceID;
IF OBJECT_ID('sp_AddAuditLog',              'P') IS NOT NULL DROP PROCEDURE sp_AddAuditLog;
IF OBJECT_ID('sp_GetAuditLogs',             'P') IS NOT NULL DROP PROCEDURE sp_GetAuditLogs;
IF OBJECT_ID('sp_AddResultHistory',         'P') IS NOT NULL DROP PROCEDURE sp_AddResultHistory;
IF OBJECT_ID('sp_GetPendingResultCount',    'P') IS NOT NULL DROP PROCEDURE sp_GetPendingResultCount;
GO

PRINT '';
PRINT '============================================================';
PRINT '  PHẦN 1: TRIGGERS (6)';
PRINT '============================================================';
PRINT '';
GO

-- ────────────────────────────────────────────────────────────
-- TRIGGER 1: Tự động tính IsWarning khi INSERT/UPDATE TestResults
-- So sánh ResultValue với RegulationLimits (MinValue, MaxValue)
-- Dùng RegulationID từ Samples + ParamID từ TestResults để
-- tra cứu chính xác ngưỡng QCVN tương ứng.
-- ────────────────────────────────────────────────────────────
CREATE TRIGGER trg_TestResults_AutoWarning
ON TestResults
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE tr
    SET tr.IsWarning =
        CASE
            WHEN rl.LimitID IS NULL                                      THEN 0
            WHEN rl.MinValue IS NOT NULL AND i.ResultValue < rl.MinValue THEN 1
            WHEN rl.MaxValue IS NOT NULL AND i.ResultValue > rl.MaxValue THEN 1
            ELSE 0
        END
    FROM TestResults tr
    INNER JOIN inserted i ON tr.ResultID = i.ResultID
    INNER JOIN Samples  s ON tr.SampleID = s.SampleID
    LEFT JOIN RegulationLimits rl
        ON rl.RegulationID = s.RegulationID
        AND rl.ParamID     = tr.ParamID;
END;
GO
PRINT '  [OK] trg_TestResults_AutoWarning';
GO

-- ────────────────────────────────────────────────────────────
-- TRIGGER 2: Ghi lịch sử thay đổi kết quả vào ResultHistory
-- Chỉ khi UPDATE và ResultValue thực sự thay đổi.
-- Đọc lý do sửa đổi từ SESSION_CONTEXT (được sp_SaveTestResult set trước).
-- ────────────────────────────────────────────────────────────
CREATE TRIGGER trg_TestResults_AuditHistory
ON TestResults
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Ghi lịch sử khi ResultValue thay đổi: lưu ai sửa, lúc nào, giá trị cũ/mới
    INSERT INTO ResultHistory
        (HistoryID, ResultID, OldValue, NewValue, ChangedBy, ChangedAt)
    SELECT
        NEWID(),
        i.ResultID,
        d.ResultValue,
        i.ResultValue,
        i.TesterID,
        GETDATE()
    FROM inserted i
    INNER JOIN deleted d ON i.ResultID = d.ResultID
    WHERE i.ResultValue <> d.ResultValue;
END;
GO
PRINT '  [OK] trg_TestResults_AuditHistory';
GO

-- ────────────────────────────────────────────────────────────
-- TRIGGER 3: Tự động cập nhật UpdatedAt — Users
-- ────────────────────────────────────────────────────────────
CREATE TRIGGER trg_Users_UpdatedAt
ON Users
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE u SET u.UpdatedAt = SYSDATETIME()
    FROM Users u INNER JOIN inserted i ON u.UserID = i.UserID;
END;
GO
PRINT '  [OK] trg_Users_UpdatedAt';
GO

-- ────────────────────────────────────────────────────────────
-- TRIGGER 4: Tự động cập nhật UpdatedAt — Contracts
-- ────────────────────────────────────────────────────────────
CREATE TRIGGER trg_Contracts_UpdatedAt
ON Contracts
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE c SET c.UpdatedAt = SYSDATETIME()
    FROM Contracts c INNER JOIN inserted i ON c.ContractID = i.ContractID;
END;
GO
PRINT '  [OK] trg_Contracts_UpdatedAt';
GO

-- ────────────────────────────────────────────────────────────
-- TRIGGER 5: Tự động cập nhật UpdatedAt — Orders
-- ────────────────────────────────────────────────────────────
CREATE TRIGGER trg_Orders_UpdatedAt
ON Orders
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE o SET o.UpdatedAt = SYSDATETIME()
    FROM Orders o INNER JOIN inserted i ON o.OrderID = i.OrderID;
END;
GO
PRINT '  [OK] trg_Orders_UpdatedAt';
GO

-- ────────────────────────────────────────────────────────────
-- TRIGGER 6: Tự động cập nhật UpdatedAt — Samples
-- ────────────────────────────────────────────────────────────
CREATE TRIGGER trg_Samples_UpdatedAt
ON Samples
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE s SET s.UpdatedAt = SYSDATETIME()
    FROM Samples s INNER JOIN inserted i ON s.SampleID = i.SampleID;
END;
GO
PRINT '  [OK] trg_Samples_UpdatedAt';
GO

PRINT '';
PRINT '============================================================';
PRINT '  PHẦN 2: SCALAR FUNCTIONS (4)';
PRINT '============================================================';
PRINT '';
GO

-- ────────────────────────────────────────────────────────────
-- FUNCTION 1: Kiểm tra giá trị kết quả có vượt ngưỡng QCVN
-- Input : @RegulationID, @ParamID, @ResultValue
-- Output: 1 = vượt ngưỡng, 0 = trong ngưỡng / không có ngưỡng
-- ────────────────────────────────────────────────────────────
CREATE FUNCTION dbo.fn_IsResultExceedingLimit
(
    @RegulationID VARCHAR(50),
    @ParamID      VARCHAR(50),
    @ResultValue  FLOAT
)
RETURNS BIT
AS
BEGIN
    DECLARE @minVal FLOAT, @maxVal FLOAT;

    SELECT @minVal = MinValue, @maxVal = MaxValue
    FROM RegulationLimits
    WHERE RegulationID = @RegulationID AND ParamID = @ParamID;

    IF @minVal IS NULL AND @maxVal IS NULL RETURN 0;
    IF @minVal IS NOT NULL AND @ResultValue < @minVal RETURN 1;
    IF @maxVal IS NOT NULL AND @ResultValue > @maxVal RETURN 1;
    RETURN 0;
END;
GO
PRINT '  [OK] fn_IsResultExceedingLimit';
GO

-- ────────────────────────────────────────────────────────────
-- FUNCTION 2: Xác định trạng thái hợp đồng dựa trên ngày hết hạn
-- Output: 0=Active, 2=Expiring (sắp hết ≤@DaysWarning ngày), 3=Expired
-- ────────────────────────────────────────────────────────────
CREATE FUNCTION dbo.fn_GetContractStatus
(
    @ValidTo     DATETIME,
    @DaysWarning INT = 30
)
RETURNS INT
AS
BEGIN
    IF @ValidTo < GETDATE()
        RETURN 3;   -- Expired
    IF @ValidTo <= DATEADD(DAY, @DaysWarning, GETDATE())
        RETURN 2;   -- Expiring
    RETURN 0;       -- Active
END;
GO
PRINT '  [OK] fn_GetContractStatus';
GO

-- ────────────────────────────────────────────────────────────
-- FUNCTION 3: Tính % hoàn thành của 1 Order
-- Dựa trên tỉ lệ Samples có Status = 2 (Hoàn thành) / tổng Samples
-- ────────────────────────────────────────────────────────────
CREATE FUNCTION dbo.fn_GetOrderCompletionPercent
(
    @OrderID VARCHAR(50)
)
RETURNS DECIMAL(5,2)
AS
BEGIN
    DECLARE @total INT, @done INT;
    SELECT @total = COUNT(*),
           @done  = SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)
    FROM Samples
    WHERE OrderID = @OrderID;

    IF ISNULL(@total, 0) = 0 RETURN 0;
    RETURN CAST(@done AS DECIMAL(5,2)) / CAST(@total AS DECIMAL(5,2)) * 100;
END;
GO
PRINT '  [OK] fn_GetOrderCompletionPercent';
GO

-- ────────────────────────────────────────────────────────────
-- FUNCTION 4: Format ContractID tự động (HD-XXXX)
-- Input : số thứ tự (INT)
-- Output: 'HD-0001', 'HD-0042', v.v.
-- ────────────────────────────────────────────────────────────
CREATE FUNCTION dbo.fn_FormatContractID
(
    @Sequence INT
)
RETURNS VARCHAR(10)
AS
BEGIN
    RETURN 'HD-' + RIGHT('000' + CAST(@Sequence AS VARCHAR(7)), 4);
END;
GO
PRINT '  [OK] fn_FormatContractID';
GO

PRINT '';
PRINT '============================================================';
PRINT '  PHẦN 3: TABLE-VALUED FUNCTIONS (2)';
PRINT '============================================================';
PRINT '';
GO

-- ────────────────────────────────────────────────────────────
-- FUNCTION 5 (TVF): Lấy danh sách thông số theo Regulation kèm ngưỡng
-- ────────────────────────────────────────────────────────────
CREATE FUNCTION dbo.fn_GetParametersByRegulation
(
    @RegulationID VARCHAR(50)
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        tp.ParamID,
        tp.ParamName,
        tp.Unit,
        tp.TestMethod,
        tp.IsField,
        tp.Price,
        rl.MinValue,
        rl.MaxValue
    FROM TestParameters tp
    INNER JOIN RegulationLimits rl ON tp.ParamID = rl.ParamID
    WHERE rl.RegulationID = @RegulationID
);
GO
PRINT '  [OK] fn_GetParametersByRegulation';
GO

-- ────────────────────────────────────────────────────────────
-- FUNCTION 6 (TVF): Lấy kết quả phân tích kèm ngưỡng QCVN cho 1 mẫu
-- ────────────────────────────────────────────────────────────
CREATE FUNCTION dbo.fn_GetTestResultsWithLimits
(
    @SampleID VARCHAR(50)
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        tr.ResultID,
        tr.SampleID,
        tp.ParamID,
        tp.ParamName,
        tp.Unit,
        tp.TestMethod,
        tp.IsField,
        tr.ResultValue,
        rl.MinValue  AS QcvnMin,
        rl.MaxValue  AS QcvnMax,
        tr.IsWarning,
        u.FullName   AS TesterName,
        tr.EnteredAt
    FROM TestResults tr
    INNER JOIN TestParameters    tp ON tr.ParamID  = tp.ParamID
    INNER JOIN Samples            s ON tr.SampleID  = s.SampleID
    LEFT JOIN  RegulationLimits  rl ON rl.RegulationID = s.RegulationID
                                    AND rl.ParamID     = tr.ParamID
    LEFT JOIN  Users              u ON tr.TesterID  = u.UserID
    WHERE tr.SampleID = @SampleID
);
GO
PRINT '  [OK] fn_GetTestResultsWithLimits';
GO

PRINT '';
PRINT '============================================================';
PRINT '  PHẦN 4: STORED PROCEDURES — CORE BUSINESS (5)';
PRINT '============================================================';
PRINT '';
GO

-- ────────────────────────────────────────────────────────────
-- SP 1: Lưu kết quả phân tích (INSERT hoặc UPDATE)
-- - Trigger trg_TestResults_AutoWarning tự tính IsWarning
-- - Trigger trg_TestResults_AuditHistory tự ghi lịch sử (đọc ChangeReason từ SESSION_CONTEXT)
-- - SP trả về bản ghi đã cập nhật để C# biết IsWarning
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_SaveTestResult
    @ResultID    VARCHAR(50),
    @SampleID    VARCHAR(50),
    @ParamID     VARCHAR(50),
    @ResultValue FLOAT,
    @TesterID    VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM TestResults WHERE ResultID = @ResultID)
    BEGIN
        -- Sửa kết quả đã tồn tại
        -- Trigger trg_TestResults_AuditHistory tự ghi ResultHistory (ChangedBy, ChangedAt, OldValue, NewValue)
        -- Trigger trg_TestResults_AutoWarning tự recalc IsWarning
        UPDATE TestResults
        SET ResultValue = @ResultValue,
            TesterID    = @TesterID
        WHERE ResultID = @ResultID;
    END
    ELSE
    BEGIN
        -- Lần đầu nhập — trigger sẽ tự tính IsWarning
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt)
        VALUES (@ResultID, @SampleID, @ParamID, @ResultValue, 0, @TesterID, GETDATE());
    END

    -- Trả về bản ghi sau khi trigger đã cập nhật IsWarning
    SELECT
        tr.ResultID,
        tr.SampleID,
        tr.ParamID,
        tr.ResultValue,
        tr.IsWarning,
        tr.TesterID,
        tr.EnteredAt
    FROM TestResults tr
    WHERE tr.ResultID = @ResultID;
END;
GO
PRINT '  [OK] sp_SaveTestResult';
GO

-- ────────────────────────────────────────────────────────────
-- SP 2: Sinh mã nhân viên mới (atomic — tránh race condition)
-- Input : @RoleID
-- Output: @UserID, @EmployeeCode, @Username (OUTPUT params)
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_GenerateEmployeeCode
    @RoleID       VARCHAR(50),
    @UserID       VARCHAR(10)  OUTPUT,
    @EmployeeCode VARCHAR(20)  OUTPUT,
    @Username     VARCHAR(50)  OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- UserID: dựa trên global count toàn bảng (U01, U02, ...)
    DECLARE @GlobalSeq INT;
    SELECT @GlobalSeq = ISNULL(MAX(TRY_CAST(SUBSTRING(UserID, 2, LEN(UserID) - 1) AS INT)), 0) + 1
    FROM Users
    WHERE UserID LIKE 'U%';
    SET @UserID = 'U' + RIGHT('00' + CAST(@GlobalSeq AS VARCHAR(5)), 2);

    -- Prefix mapping theo RoleID
    DECLARE @EmpPfx  VARCHAR(10) = CASE @RoleID
        WHEN 'R01' THEN 'ADM' WHEN 'R02' THEN 'DIR' WHEN 'R03' THEN 'SAL'
        WHEN 'R04' THEN 'FLD' WHEN 'R05' THEN 'LAB' WHEN 'R06' THEN 'PLN'
        WHEN 'R07' THEN 'RST' ELSE 'EMP' END;

    DECLARE @UserPfx VARCHAR(20) = CASE @RoleID
        WHEN 'R01' THEN 'admin'    WHEN 'R02' THEN 'director' WHEN 'R03' THEN 'sale'
        WHEN 'R04' THEN 'field'    WHEN 'R05' THEN 'lab'      WHEN 'R06' THEN 'plan'
        WHEN 'R07' THEN 'result'   ELSE 'user' END;

    DECLARE @EmpCount  INT, @UserCount INT;
    SELECT @EmpCount  = COUNT(*) FROM Users WHERE EmployeeCode LIKE @EmpPfx  + '%';
    SELECT @UserCount = COUNT(*) FROM Users WHERE Username      LIKE @UserPfx + '%';

    SET @EmployeeCode = @EmpPfx  + RIGHT('000' + CAST(@EmpCount  + 1 AS VARCHAR(5)), 3);
    SET @Username     = @UserPfx + RIGHT('00'  + CAST(@UserCount + 1 AS VARCHAR(5)), 2);
END;
GO
PRINT '  [OK] sp_GenerateEmployeeCode';
GO

-- ────────────────────────────────────────────────────────────
-- SP 3: UPSERT nhân viên
-- - Email đã tồn tại + IsActive=1  → THROW 50002 (lỗi)
-- - Email đã tồn tại + IsActive=0  → kích hoạt lại
-- - Email mới                       → INSERT
-- Output: @Action = 'INSERT' | 'UPDATE'
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_UpsertEmployee
    @UserID       VARCHAR(50),
    @Username     VARCHAR(50),
    @PasswordHash VARCHAR(255),
    @FullName     NVARCHAR(100),
    @Email        VARCHAR(100),
    @RoleID       VARCHAR(50),
    @Phone        VARCHAR(20)      = NULL,
    @Address      NVARCHAR(255)    = NULL,
    @Department   NVARCHAR(100)    = NULL,
    @DateOfBirth  DATETIME         = NULL,
    @EmployeeCode VARCHAR(20),
    @AvatarData   VARBINARY(MAX)   = NULL,
    @Action       VARCHAR(10)      OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @ExID     VARCHAR(50);
        DECLARE @ExActive BIT;

        SELECT @ExID = UserID, @ExActive = IsActive
        FROM Users
        WHERE Email = @Email;

        IF @ExID IS NOT NULL AND @ExActive = 1
            THROW 50002, N'Email đã được sử dụng bởi nhân viên đang hoạt động.', 1;

        IF @ExID IS NOT NULL AND @ExActive = 0
        BEGIN
            -- Kích hoạt lại nhân viên đã bị vô hiệu hóa
            UPDATE Users SET
                FullName = @FullName, Username   = @Username,   PasswordHash = @PasswordHash,
                Department = @Department,  Phone = @Phone,      EmployeeCode = @EmployeeCode,
                Address  = @Address, RoleID      = @RoleID,     DateOfBirth  = @DateOfBirth,
                IsActive = 1,
                AvatarData = ISNULL(@AvatarData, AvatarData)   -- giữ ảnh cũ nếu không có ảnh mới
            WHERE Email = @Email;
            SET @Action = 'UPDATE';
        END
        ELSE
        BEGIN
            INSERT INTO Users
                (UserID, Username, PasswordHash, FullName, Email, RoleID, IsActive,
                 CreatedDate, Phone, Address, Department, DateOfBirth, EmployeeCode, AvatarData)
            VALUES
                (@UserID, @Username, @PasswordHash, @FullName, @Email, @RoleID, 1,
                 GETDATE(), @Phone, @Address, @Department, @DateOfBirth, @EmployeeCode, @AvatarData);
            SET @Action = 'INSERT';
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
PRINT '  [OK] sp_UpsertEmployee';
GO

-- ────────────────────────────────────────────────────────────
-- SP 4: Xóa toàn bộ khu vực lấy mẫu (cascade 5 bảng, atomic)
-- Tuân thủ FK: ResultHistory → TestResults → Samples
--                           → SamplingPlanItems → Orders
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_DeleteSamplingArea
    @OrderID VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        -- 1. Xóa history của các kết quả trong khu vực
        DELETE rh
        FROM ResultHistory rh
        INNER JOIN TestResults tr ON rh.ResultID = tr.ResultID
        INNER JOIN Samples      s ON tr.SampleID  = s.SampleID
        WHERE s.OrderID = @OrderID;

        -- 2. Xóa kết quả phân tích
        DELETE tr
        FROM TestResults tr
        INNER JOIN Samples s ON tr.SampleID = s.SampleID
        WHERE s.OrderID = @OrderID;

        -- 3. Xóa mẫu
        DELETE FROM Samples         WHERE OrderID = @OrderID;

        -- 4. Xóa kế hoạch thông số
        DELETE FROM SamplingPlanItems WHERE OrderID = @OrderID;

        -- 5. Xóa khu vực (Order)
        DELETE FROM Orders WHERE OrderID = @OrderID;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
PRINT '  [OK] sp_DeleteSamplingArea';
GO

-- ────────────────────────────────────────────────────────────
-- SP 5: Lưu kế hoạch lấy mẫu cho 1 khu vực (atomic)
-- - Xóa plan cũ
-- - Update Order.Status = 1 (Đã lập kế hoạch)
-- - Insert items mới từ JSON array
-- JSON format: [{"ParamID":"P1","RegulationID":"R1","Department":"Hiện trường","QcvnLimit":"≤ 50"}]
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_SaveSamplingPlan
    @OrderID    VARCHAR(50),
    @ItemsJson  NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        -- Xóa kế hoạch cũ của khu vực
        DELETE FROM SamplingPlanItems WHERE OrderID = @OrderID;

        -- Cập nhật Order Status = 1 (Đã lập kế hoạch)
        UPDATE Orders SET Status = 1 WHERE OrderID = @OrderID;

        -- Thêm thông số tuỳ biến mới vào TestParameters nếu chưa có (tránh lỗi FK)
        INSERT INTO TestParameters (ParamID, ParamName, Unit, TestMethod, IsField, Price)
        SELECT DISTINCT 
            j.ParamID, 
            ISNULL(NULLIF(j.ParamName, ''), N'Custom Parameter'), 
            ISNULL(j.Unit, ''), 
            '', 
            CASE WHEN j.Department = N'Hiện trường' THEN 1 ELSE 0 END, 
            0
        FROM OPENJSON(@ItemsJson)
        WITH (
            ParamID      VARCHAR(50)   '$.ParamID',
            ParamName    NVARCHAR(100) '$.ParamName',
            Unit         NVARCHAR(20)  '$.Unit',
            Department   NVARCHAR(50)  '$.Department'
        ) AS j
        WHERE NOT EXISTS (SELECT 1 FROM TestParameters t WHERE t.ParamID = j.ParamID);

        -- Insert từ JSON
        INSERT INTO SamplingPlanItems
            (PlanItemID, OrderID, ParamID, RegulationID, Department, QcvnLimit)
        SELECT
            LOWER(NEWID()),
            @OrderID,
            j.ParamID,
            NULLIF(j.RegulationID, ''),
            j.Department,
            j.QcvnLimit
        FROM OPENJSON(@ItemsJson)
        WITH (
            ParamID      VARCHAR(50)   '$.ParamID',
            RegulationID VARCHAR(50)   '$.RegulationID',
            Department   NVARCHAR(50)  '$.Department',
            QcvnLimit    NVARCHAR(100) '$.QcvnLimit'
        ) AS j;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
PRINT '  [OK] sp_SaveSamplingPlan';
GO

-- ────────────────────────────────────────────────────────────
-- SP 6: Đăng ký / cập nhật FaceID (atomic — thay manual transaction C#)
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_RegisterFaceID
    @UserID   VARCHAR(50),
    @FaceData VARBINARY(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE Users
        SET FaceIDData = @FaceData, IsFaceIDRegistered = 1
        WHERE UserID = @UserID;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
PRINT '  [OK] sp_RegisterFaceID';
GO

PRINT '';
PRINT '============================================================';
PRINT '  PHẦN 5: STORED PROCEDURES — QUERIES & REPORTS (14)';
PRINT '============================================================';
PRINT '';
GO

-- ────────────────────────────────────────────────────────────
-- SP 7: Dashboard tổng quan cho Giám đốc (4 result sets)
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_GetDashboardStats
AS
BEGIN
    SET NOCOUNT ON;

    -- RS1: Thống kê tổng quan
    SELECT
        (SELECT COUNT(*) FROM Contracts  WHERE Status = 0)            AS ActiveContracts,
        (SELECT COUNT(*) FROM Contracts  WHERE Status = 2)            AS ExpiringContracts,
        (SELECT COUNT(*) FROM Contracts  WHERE Status = 3)            AS ExpiredContracts,
        (SELECT COUNT(*) FROM Orders     WHERE Status = 1)            AS InProgressOrders,
        (SELECT COUNT(*) FROM Orders     WHERE Status = 2)            AS CompletedOrders,
        (SELECT COUNT(*) FROM Samples)                                AS TotalSamples,
        (SELECT COUNT(*) FROM TestResults WHERE IsWarning = 1)        AS WarningResults,
        (SELECT COUNT(*) FROM Users      WHERE IsActive  = 1)         AS ActiveUsers,
        (SELECT COUNT(DISTINCT CustomerID) FROM Contracts WHERE Status IN (0,2)) AS ActiveCustomers,
        (SELECT ISNULL(SUM(TotalContractValue),0) FROM Contracts WHERE Status IN (0,2)) AS TotalActiveRevenue;

    -- RS2: Top 5 hợp đồng sắp hết hạn
    SELECT TOP 5
        c.ContractID,
        cu.CompanyName,
        c.ValidTo,
        DATEDIFF(DAY, GETDATE(), c.ValidTo) AS DaysRemaining,
        c.TotalContractValue
    FROM Contracts c
    INNER JOIN Customers cu ON c.CustomerID = cu.CustomerID
    WHERE c.ValidTo > GETDATE() AND c.Status IN (0, 2)
    ORDER BY c.ValidTo ASC;

    -- RS3: Phân bổ doanh thu theo ngành
    SELECT
        ISNULL(IndustryType, N'Chưa phân loại') AS IndustryType,
        COUNT(*)                AS ContractCount,
        SUM(TotalContractValue) AS TotalRevenue,
        AVG(TotalContractValue) AS AvgRevenue
    FROM Contracts
    WHERE Status IN (0, 2)
    GROUP BY IndustryType
    ORDER BY TotalRevenue DESC;

    -- RS4: Top 10 thông số có cảnh báo nhiều nhất
    SELECT TOP 10
        tp.ParamID,
        tp.ParamName,
        tp.Unit,
        COUNT(*) AS WarningCount
    FROM TestResults tr
    INNER JOIN TestParameters tp ON tr.ParamID = tp.ParamID
    WHERE tr.IsWarning = 1
    GROUP BY tp.ParamID, tp.ParamName, tp.Unit
    ORDER BY WarningCount DESC;
END;
GO
PRINT '  [OK] sp_GetDashboardStats';
GO

-- ────────────────────────────────────────────────────────────
-- SP 8: Danh sách hợp đồng dạng card (Phòng Kinh doanh)
-- Thay ContractRepository.GetContractCardsAsync()
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_GetContractCards
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.ContractID          AS ContractId,
        cu.CompanyName,
        cu.Representative,
        cu.PhoneNumber,
        c.SignedDate,
        c.ValidTo,
        cu.Address,
        c.Status,
        c.CustomerID          AS CustomerId,
        c.TotalContractValue,
        c.IndustryType,
        DATEDIFF(DAY, GETDATE(), c.ValidTo)              AS DaysRemaining,
        -- Dữ liệu thực từ CustomerFeedbacks cho AI scoring
        ISNULL(cf.ResponseTime,       72)                AS ResponseTime,        -- giờ; default 72h
        ISNULL(cf.PreviousViolations,  0)                AS PreviousViolations   -- số lần vi phạm
    FROM Contracts c
    INNER JOIN Customers       cu  ON c.CustomerID  = cu.CustomerID
    LEFT JOIN  CustomerFeedbacks cf ON cf.CustomerID = c.CustomerID
    ORDER BY c.SignedDate DESC;
END;
GO
PRINT '  [OK] sp_GetContractCards';
GO

-- ────────────────────────────────────────────────────────────
-- SP 9: Tìm kiếm hợp đồng theo từ khóa
-- Thay ContractRepository.SearchContractCardsAsync()
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_SearchContracts
    @Keyword NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Pat NVARCHAR(202) = '%' + @Keyword + '%';
    SELECT
        c.ContractID          AS ContractId,
        cu.CompanyName,
        cu.Representative,
        cu.PhoneNumber,
        c.SignedDate,
        c.ValidTo,
        cu.Address,
        c.Status,
        c.CustomerID          AS CustomerId,
        c.TotalContractValue,
        c.IndustryType,
        -- Real AI data (consistent với sp_GetContractCards)
        ISNULL(cf.ResponseTime,      72) AS ResponseTime,
        ISNULL(cf.PreviousViolations,  0) AS PreviousViolations
    FROM Contracts c
    INNER JOIN Customers        cu  ON c.CustomerID  = cu.CustomerID
    LEFT JOIN  CustomerFeedbacks cf  ON cf.CustomerID = c.CustomerID
    WHERE cu.CompanyName    LIKE @Pat
       OR c.ContractID      LIKE @Pat
       OR cu.TaxCode        LIKE @Pat
       OR cu.Representative LIKE @Pat
    ORDER BY c.SignedDate DESC;
END;
GO
PRINT '  [OK] sp_SearchContracts';
GO

-- ────────────────────────────────────────────────────────────
-- SP 10: Hợp đồng sắp hết hạn và đã quá hạn cho notification
-- Thay ContractRepository.GetExpiringContractEmailsAsync() +
--      ContractRepository.GetContractsForNotificationAsync()
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_GetExpiringContracts
    @DaysThreshold INT = 30
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.ContractID          AS ContractId,
        cu.CompanyName,
        cu.ContactEmail,
        cu.Representative,
        c.ValidFrom           AS SignedDate,
        c.ValidTo,
        c.Status,
        DATEDIFF(DAY, GETDATE(), c.ValidTo) AS DaysRemaining,
        c.TotalContractValue
    FROM Contracts c
    INNER JOIN Customers cu ON c.CustomerID = cu.CustomerID
    WHERE c.ValidTo <= DATEADD(DAY, @DaysThreshold, GETDATE())  -- bao gồm đã quá hạn
      AND c.Status IN (0, 2, 3)
    ORDER BY c.ValidTo ASC;
END;
GO
PRINT '  [OK] sp_GetExpiringContracts';
GO

-- ────────────────────────────────────────────────────────────
-- SP 11: Batch cập nhật trạng thái hợp đồng
-- Gọi bởi Hangfire background job hàng ngày lúc 0:00
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_UpdateContractStatuses
    @ExpiringDays INT = 30
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @cntExpired INT = 0, @cntExpiring INT = 0;

    UPDATE Contracts SET Status = 3
    WHERE ValidTo < GETDATE() AND Status IN (0, 2);
    SET @cntExpired = @@ROWCOUNT;

    UPDATE Contracts SET Status = 2
    WHERE ValidTo BETWEEN GETDATE() AND DATEADD(DAY, @ExpiringDays, GETDATE())
      AND Status = 0;
    SET @cntExpiring = @@ROWCOUNT;

    SELECT @cntExpired AS ExpiredCount, @cntExpiring AS ExpiringCount;
END;
GO
PRINT '  [OK] sp_UpdateContractStatuses';
GO

-- ────────────────────────────────────────────────────────────
-- SP 12: Orders theo ContractID kèm metadata
-- Thay ContractRepository.GetOrdersByContractIdAsync()
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_GetOrdersByContract
    @ContractID VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        o.OrderID, o.ContractID, o.OrderName, o.OrderDate,
        o.Deadline, o.FinalReportPath, o.IsApproved, o.Status, o.CreatedBy,
        o.EnvironmentType,
        u.FullName AS CreatedByName,
        (SELECT COUNT(*) FROM Samples s WHERE s.OrderID = o.OrderID) AS SampleCount,
        dbo.fn_GetOrderCompletionPercent(o.OrderID)                   AS CompletionPercent
    FROM Orders o
    LEFT JOIN Users u ON o.CreatedBy = u.UserID
    WHERE o.ContractID = @ContractID
    ORDER BY
        CASE ISNULL(o.EnvironmentType, N'Không khí')
            WHEN N'Không khí' THEN 0
            WHEN N'Nước thải' THEN 1
            WHEN N'Đất'       THEN 2
            ELSE 3
        END,
        o.OrderDate DESC;
END;
GO
PRINT '  [OK] sp_GetOrdersByContract';
GO

-- ────────────────────────────────────────────────────────────
-- SP 13: Kết quả phân tích theo Order (Phòng Kết quả / GĐ duyệt)
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_GetTestResultsByOrder
    @OrderID VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        tr.ResultID, s.SampleID, s.Barcode, s.SamplingLocation, s.SamplingTime,
        tp.ParamID, tp.ParamName, tp.Unit, tp.TestMethod, tp.IsField,
        tr.ResultValue, rl.MinValue AS QcvnMin, rl.MaxValue AS QcvnMax,
        tr.IsWarning, u.FullName AS TesterName, tr.EnteredAt,
        r.Code AS RegulationCode, r.Name AS RegulationName
    FROM TestResults tr
    INNER JOIN Samples          s  ON tr.SampleID      = s.SampleID
    INNER JOIN TestParameters   tp ON tr.ParamID        = tp.ParamID
    LEFT JOIN  RegulationLimits rl ON rl.RegulationID   = s.RegulationID
                                   AND rl.ParamID        = tr.ParamID
    LEFT JOIN  Regulations      r  ON s.RegulationID    = r.RegulationID
    LEFT JOIN  Users            u  ON tr.TesterID        = u.UserID
    WHERE s.OrderID = @OrderID
    ORDER BY s.SampleID,
             CASE WHEN tp.IsField = 1 THEN 0 ELSE 1 END,
             tp.ParamName;
END;
GO
PRINT '  [OK] sp_GetTestResultsByOrder';
GO

-- ────────────────────────────────────────────────────────────
-- SP 14: Kế hoạch lấy mẫu theo Order
-- Thay SamplingPlanRepository.GetPlanItemsByOrderAsync()
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_GetSamplingPlanByOrder
    @OrderID VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        spi.PlanItemID, spi.OrderID, spi.ParamID,
        tp.ParamName, tp.Unit, tp.TestMethod, tp.IsField, tp.Price,
        spi.RegulationID, r.Code AS RegulationCode,
        spi.Department, spi.QcvnLimit
    FROM SamplingPlanItems spi
    INNER JOIN TestParameters tp ON spi.ParamID     = tp.ParamID
    LEFT JOIN  Regulations     r ON spi.RegulationID = r.RegulationID
    WHERE spi.OrderID = @OrderID
    ORDER BY CASE WHEN spi.Department = N'Hiện trường' THEN 0 ELSE 1 END,
             tp.ParamName;
END;
GO
PRINT '  [OK] sp_GetSamplingPlanByOrder';
GO

-- ────────────────────────────────────────────────────────────
-- SP 15: Tất cả kết quả vượt ngưỡng QCVN
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_GetWarningResults
    @ContractID VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.ContractID, cu.CompanyName,
        o.OrderID, o.OrderName,
        s.SampleID, s.Barcode, s.SamplingLocation,
        tp.ParamID, tp.ParamName, tp.Unit,
        tr.ResultValue, rl.MinValue AS QcvnMin, rl.MaxValue AS QcvnMax,
        tr.EnteredAt, u.FullName AS TesterName
    FROM TestResults tr
    INNER JOIN Samples          s  ON tr.SampleID     = s.SampleID
    INNER JOIN Orders           o  ON s.OrderID        = o.OrderID
    INNER JOIN Contracts        c  ON o.ContractID     = c.ContractID
    INNER JOIN Customers        cu ON c.CustomerID     = cu.CustomerID
    INNER JOIN TestParameters   tp ON tr.ParamID       = tp.ParamID
    LEFT JOIN  RegulationLimits rl ON rl.RegulationID  = s.RegulationID
                                   AND rl.ParamID       = tr.ParamID
    LEFT JOIN  Users            u  ON tr.TesterID       = u.UserID
    WHERE tr.IsWarning = 1
      AND (@ContractID IS NULL OR c.ContractID = @ContractID)
    ORDER BY tr.EnteredAt DESC;
END;
GO
PRINT '  [OK] sp_GetWarningResults';
GO

-- ────────────────────────────────────────────────────────────
-- SP 16: Doanh thu theo quý (biểu đồ dashboard)
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_GetRevenueByQuarter
    @YearStart INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @YearStart IS NULL SET @YearStart = YEAR(GETDATE()) - 1;

    SELECT
        YEAR(SignedDate)                     AS [Year],
        DATEPART(QUARTER, SignedDate)         AS [Quarter],
        'Q' + CAST(DATEPART(QUARTER, SignedDate) AS VARCHAR)
            + '/' + CAST(YEAR(SignedDate) AS VARCHAR) AS QuarterLabel,
        COUNT(*)                             AS ContractCount,
        ISNULL(SUM(TotalContractValue), 0)   AS TotalRevenue,
        ISNULL(AVG(TotalContractValue), 0)   AS AvgRevenue
    FROM Contracts
    WHERE YEAR(SignedDate) >= @YearStart
    GROUP BY YEAR(SignedDate), DATEPART(QUARTER, SignedDate)
    ORDER BY [Year], [Quarter];
END;
GO
PRINT '  [OK] sp_GetRevenueByQuarter';
GO

-- ────────────────────────────────────────────────────────────
-- SP 17: Danh sách nhân viên kèm vai trò + avatar
-- Thay EmployeeRepository.GetAllEmployeesAsync() + SearchEmployeesAsync()
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_GetEmployeeList
    @Keyword  NVARCHAR(100) = NULL,
    @RoleID   VARCHAR(50)   = NULL,
    @IsActive BIT           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Pat NVARCHAR(102) = '%' + ISNULL(@Keyword, '') + '%';

    SELECT
        u.UserID, u.EmployeeCode, u.Username, u.FullName,
        u.Email, u.Phone, u.Department, u.DateOfBirth, u.Address,
        u.IsActive, u.CreatedDate, u.AvatarData,
        r.RoleID, r.RoleCode, r.RoleName,
        u.IsFaceIDRegistered
    FROM Users u
    INNER JOIN Roles r ON u.RoleID = r.RoleID
    WHERE (@Keyword IS NULL OR
           u.FullName    LIKE @Pat OR u.Username LIKE @Pat OR
           u.Email       LIKE @Pat OR u.EmployeeCode LIKE @Pat)
      AND (@RoleID   IS NULL OR u.RoleID   = @RoleID)
      AND (@IsActive IS NULL OR u.IsActive = @IsActive)
    ORDER BY u.EmployeeCode;
END;
GO
PRINT '  [OK] sp_GetEmployeeList';
GO

-- ────────────────────────────────────────────────────────────
-- SP 18: Xác thực người dùng — trả về user + role + password hash
-- C# nhận hash về rồi tự verify BCrypt (không bao giờ verify trong DB)
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_AuthenticateUser
    @Username VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.UserID, u.Username, u.PasswordHash, u.FullName, u.Email,
        u.Phone, u.Department, u.Address, u.DateOfBirth, u.EmployeeCode,
        u.AvatarData, u.FaceIDData, u.IsFaceIDRegistered,
        u.IsActive, u.CreatedDate,
        r.RoleID, r.RoleCode, r.RoleName
    FROM Users u
    INNER JOIN Roles r ON u.RoleID = r.RoleID
    WHERE u.Username = @Username
      AND u.IsActive = 1;
END;
GO
PRINT '  [OK] sp_AuthenticateUser';
GO

-- ────────────────────────────────────────────────────────────
-- SP 19: Báo cáo tổng hợp 1 hợp đồng (4 result sets) — export PDF
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_GetContractReport
    @ContractID VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    -- RS1: Thông tin hợp đồng + khách hàng
    SELECT
        c.ContractID, c.SignedDate, c.ValidFrom, c.ValidTo,
        c.Status, c.TotalContractValue, c.IndustryType,
        cu.CompanyName, cu.TaxCode, cu.Address,
        cu.Representative, cu.ContactEmail, cu.PhoneNumber
    FROM Contracts c
    INNER JOIN Customers cu ON c.CustomerID = cu.CustomerID
    WHERE c.ContractID = @ContractID;

    -- RS2: Danh sách khu vực (Orders)
    SELECT
        o.OrderID, o.OrderName, o.OrderDate, o.Deadline,
        o.Status, o.IsApproved,
        dbo.fn_GetOrderCompletionPercent(o.OrderID) AS CompletionPercent
    FROM Orders o
    WHERE o.ContractID = @ContractID
    ORDER BY o.OrderDate;

    -- RS3: Toàn bộ kết quả phân tích
    SELECT
        o.OrderID, o.OrderName,
        s.SampleID, s.Barcode, s.SamplingLocation, s.SamplingTime,
        s.FieldTemperature, s.FieldHumidity, s.WeatherCondition,
        tp.ParamID, tp.ParamName, tp.Unit, tp.TestMethod, tp.IsField,
        tr.ResultValue, rl.MinValue AS QcvnMin, rl.MaxValue AS QcvnMax,
        tr.IsWarning, r.Code AS RegulationCode,
        u.FullName AS TesterName, tr.EnteredAt
    FROM TestResults tr
    INNER JOIN Samples          s  ON tr.SampleID     = s.SampleID
    INNER JOIN Orders           o  ON s.OrderID        = o.OrderID
    INNER JOIN TestParameters   tp ON tr.ParamID       = tp.ParamID
    LEFT JOIN  RegulationLimits rl ON rl.RegulationID  = s.RegulationID
                                   AND rl.ParamID       = tr.ParamID
    LEFT JOIN  Regulations      r  ON s.RegulationID   = r.RegulationID
    LEFT JOIN  Users            u  ON tr.TesterID       = u.UserID
    WHERE o.ContractID = @ContractID
    ORDER BY o.OrderName, s.SamplingLocation,
             CASE WHEN tp.IsField = 1 THEN 0 ELSE 1 END, tp.ParamName;

    -- RS4: Tóm tắt cảnh báo
    SELECT
        o.OrderName, s.SamplingLocation,
        tp.ParamName, tp.Unit,
        tr.ResultValue, rl.MaxValue AS QcvnMax, rl.MinValue AS QcvnMin,
        r.Code AS RegulationCode
    FROM TestResults tr
    INNER JOIN Samples          s  ON tr.SampleID     = s.SampleID
    INNER JOIN Orders           o  ON s.OrderID        = o.OrderID
    INNER JOIN TestParameters   tp ON tr.ParamID       = tp.ParamID
    LEFT JOIN  RegulationLimits rl ON rl.RegulationID  = s.RegulationID
                                   AND rl.ParamID       = tr.ParamID
    LEFT JOIN  Regulations      r  ON s.RegulationID   = r.RegulationID
    WHERE o.ContractID = @ContractID AND tr.IsWarning = 1
    ORDER BY o.OrderName, tp.ParamName;
END;
GO
PRINT '  [OK] sp_GetContractReport';
GO

PRINT '';
PRINT '============================================================';
PRINT '  PHẦN 6: STORED PROCEDURES — CRUD OPERATIONS';
PRINT '============================================================';
PRINT '';
GO

CREATE PROCEDURE dbo.sp_AddCustomer
    @CustomerId VARCHAR(50), @TaxCode VARCHAR(50), @CompanyName NVARCHAR(200),
    @Address NVARCHAR(255) = NULL, @Representative NVARCHAR(100) = NULL,
    @ContactEmail VARCHAR(100) = NULL, @PhoneNumber VARCHAR(20) = NULL
AS BEGIN SET NOCOUNT ON;
    INSERT INTO Customers (CustomerId, TaxCode, CompanyName, Address, Representative, ContactEmail, PhoneNumber)
    VALUES (@CustomerId, @TaxCode, @CompanyName, @Address, @Representative, @ContactEmail, @PhoneNumber);
END;
GO
CREATE PROCEDURE dbo.sp_UpdateCustomer
    @CustomerId VARCHAR(50), @CompanyName NVARCHAR(200),
    @Address NVARCHAR(255) = NULL, @Representative NVARCHAR(100) = NULL,
    @ContactEmail VARCHAR(100) = NULL, @PhoneNumber VARCHAR(20) = NULL
AS BEGIN SET NOCOUNT ON;
    UPDATE Customers SET CompanyName=@CompanyName, Address=@Address, Representative=@Representative,
        ContactEmail=@ContactEmail, PhoneNumber=@PhoneNumber WHERE CustomerId=@CustomerId;
END;
GO
CREATE PROCEDURE dbo.sp_DeleteCustomer @CustomerId VARCHAR(50) AS
BEGIN SET NOCOUNT ON; DELETE FROM Customers WHERE CustomerId=@CustomerId; END;
GO
CREATE PROCEDURE dbo.sp_GetCustomerById @CustomerId VARCHAR(50) AS
BEGIN SET NOCOUNT ON; SELECT * FROM Customers WHERE CustomerId=@CustomerId; END;
GO
CREATE PROCEDURE dbo.sp_GetAllCustomers AS
BEGIN SET NOCOUNT ON; SELECT * FROM Customers; END;
GO
CREATE PROCEDURE dbo.sp_CheckTaxCodeExists @TaxCode VARCHAR(50) AS
BEGIN SET NOCOUNT ON; SELECT COUNT(1) AS Cnt FROM Customers WHERE TaxCode=@TaxCode; END;
GO
PRINT '  [OK] Customer CRUD (6 SPs)';
GO

CREATE PROCEDURE dbo.sp_AddContract
    @ContractID VARCHAR(50), @CustomerID VARCHAR(50), @SignedDate DATETIME,
    @ValidFrom DATETIME, @ValidTo DATETIME, @ContractFilePath NVARCHAR(500) = NULL,
    @Status INT = 0, @CreatedBy VARCHAR(50) = NULL,
    @TotalContractValue DECIMAL(18,2) = 0, @IndustryType NVARCHAR(100) = NULL, @RenewalLabel NVARCHAR(50) = NULL
AS BEGIN SET NOCOUNT ON;
    INSERT INTO Contracts (ContractID,CustomerID,SignedDate,ValidFrom,ValidTo,ContractFilePath,Status,CreatedBy,TotalContractValue,IndustryType,RenewalLabel)
    VALUES (@ContractID,@CustomerID,@SignedDate,@ValidFrom,@ValidTo,@ContractFilePath,@Status,@CreatedBy,@TotalContractValue,@IndustryType,@RenewalLabel);
END;
GO
CREATE PROCEDURE dbo.sp_UpdateContract
    @ContractID VARCHAR(50), @CustomerID VARCHAR(50), @SignedDate DATETIME,
    @ValidFrom DATETIME, @ValidTo DATETIME, @ContractFilePath NVARCHAR(500) = NULL,
    @Status INT = 0, @TotalContractValue DECIMAL(18,2) = 0,
    @IndustryType NVARCHAR(100) = NULL, @RenewalLabel NVARCHAR(50) = NULL
AS BEGIN SET NOCOUNT ON;
    UPDATE Contracts SET CustomerID=@CustomerID, SignedDate=@SignedDate, ValidFrom=@ValidFrom, ValidTo=@ValidTo,
        ContractFilePath=@ContractFilePath, Status=@Status, TotalContractValue=@TotalContractValue,
        IndustryType=@IndustryType, RenewalLabel=@RenewalLabel
    WHERE ContractID=@ContractID;
END;
GO
CREATE PROCEDURE dbo.sp_DeleteContract @ContractID VARCHAR(50) AS
BEGIN SET NOCOUNT ON; DELETE FROM Contracts WHERE ContractID=@ContractID; END;
GO
CREATE PROCEDURE dbo.sp_GetContractById @ContractID VARCHAR(50) AS
BEGIN SET NOCOUNT ON;
    SELECT ContractID AS ContractId, CustomerID AS CustomerId, SignedDate, ValidFrom, ValidTo,
           ContractFilePath, Status, CreatedBy, TotalContractValue, IndustryType, RenewalLabel
    FROM Contracts WHERE ContractID=@ContractID;
END;
GO
CREATE PROCEDURE dbo.sp_GetAllContracts AS
BEGIN SET NOCOUNT ON;
    SELECT ContractID AS ContractId, CustomerID AS CustomerId, SignedDate, ValidFrom, ValidTo,
           ContractFilePath, Status, CreatedBy, TotalContractValue, IndustryType, RenewalLabel
    FROM Contracts ORDER BY SignedDate DESC;
END;
GO
CREATE PROCEDURE dbo.sp_GetContractsByCustomer @CustomerID VARCHAR(50) AS
BEGIN SET NOCOUNT ON;
    SELECT ContractID AS ContractId, CustomerID AS CustomerId, SignedDate, ValidFrom, ValidTo,
           ContractFilePath, Status, CreatedBy, TotalContractValue, IndustryType, RenewalLabel
    FROM Contracts WHERE CustomerID=@CustomerID ORDER BY SignedDate DESC;
END;
GO
CREATE PROCEDURE dbo.sp_GetContractsWithCustomerName AS
BEGIN SET NOCOUNT ON;
    SELECT c.ContractID AS ContractId, cu.CompanyName AS CustomerName, MAX(o.OrderDate) AS SampleDate
    FROM Contracts c JOIN Customers cu ON c.CustomerID=cu.CustomerID
    LEFT JOIN Orders o ON o.ContractID=c.ContractID
    GROUP BY c.ContractID, cu.CompanyName, c.SignedDate ORDER BY c.SignedDate DESC;
END;
GO
CREATE PROCEDURE dbo.sp_GetContractCardById @ContractID VARCHAR(50) AS
BEGIN SET NOCOUNT ON;
    SELECT c.ContractID AS ContractId, cu.CompanyName, cu.Representative, cu.PhoneNumber,
           c.SignedDate, c.ValidTo, cu.Address, c.Status, c.CustomerID AS CustomerId,
           c.TotalContractValue, c.IndustryType,
           ISNULL(cf.ResponseTime,      72) AS ResponseTime,
           ISNULL(cf.PreviousViolations,  0) AS PreviousViolations
    FROM Contracts c
    JOIN Customers cu ON c.CustomerID = cu.CustomerID
    LEFT JOIN CustomerFeedbacks cf ON cf.CustomerID = c.CustomerID
    WHERE c.ContractID=@ContractID;
END;
GO
PRINT '  [OK] Contract CRUD (8 SPs)';
GO

CREATE PROCEDURE dbo.sp_AddOrder
    @OrderID VARCHAR(50), @ContractID VARCHAR(50), @OrderName NVARCHAR(200), @OrderDate DATETIME,
    @Deadline DATETIME = NULL, @FinalReportPath NVARCHAR(500) = NULL, @IsApproved INT = 0, @Status INT = 0,
    @EnvironmentType NVARCHAR(50) = NULL
AS BEGIN SET NOCOUNT ON;
    INSERT INTO Orders (OrderID,ContractID,OrderName,OrderDate,Deadline,FinalReportPath,IsApproved,Status,EnvironmentType)
    VALUES (@OrderID,@ContractID,@OrderName,@OrderDate,@Deadline,@FinalReportPath,@IsApproved,@Status,@EnvironmentType);
END;
GO
CREATE PROCEDURE dbo.sp_UpdateOrder
    @OrderID VARCHAR(50), @ContractID VARCHAR(50), @OrderName NVARCHAR(200), @OrderDate DATETIME,
    @Deadline DATETIME = NULL, @FinalReportPath NVARCHAR(500) = NULL, @IsApproved INT = 0, @Status INT = 0,
    @EnvironmentType NVARCHAR(50) = NULL
AS BEGIN SET NOCOUNT ON;
    UPDATE Orders SET ContractID=@ContractID, OrderName=@OrderName, OrderDate=@OrderDate,
        Deadline=@Deadline, FinalReportPath=@FinalReportPath, IsApproved=@IsApproved, Status=@Status,
        EnvironmentType=@EnvironmentType
    WHERE OrderID=@OrderID;
END;
GO
CREATE PROCEDURE dbo.sp_DeleteOrder @OrderID VARCHAR(50) AS
BEGIN SET NOCOUNT ON; DELETE FROM Orders WHERE OrderID=@OrderID; END;
GO
CREATE PROCEDURE dbo.sp_GetOrderById @OrderID VARCHAR(50) AS
BEGIN SET NOCOUNT ON; SELECT * FROM Orders WHERE OrderID=@OrderID; END;
GO
CREATE PROCEDURE dbo.sp_GetAllOrders AS
BEGIN SET NOCOUNT ON; SELECT * FROM Orders; END;
GO
PRINT '  [OK] Order CRUD (5 SPs)';
GO

CREATE PROCEDURE dbo.sp_AddSample
    @SampleID VARCHAR(50), @OrderID VARCHAR(50), @RegulationID VARCHAR(50) = NULL,
    @Barcode VARCHAR(50) = NULL, @SamplingLocation NVARCHAR(255) = NULL, @SamplingTime DATETIME = NULL,
    @FieldTemperature FLOAT = NULL, @FieldHumidity FLOAT = NULL, @WeatherCondition NVARCHAR(100) = NULL,
    @FieldImage VARBINARY(MAX) = NULL, @IsWarning BIT = 0, @SamplerID VARCHAR(50) = NULL, @Status INT = 0
AS BEGIN SET NOCOUNT ON;
    INSERT INTO Samples (SampleID,OrderID,RegulationID,Barcode,SamplingLocation,SamplingTime,
        FieldTemperature,FieldHumidity,WeatherCondition,FieldImage,IsWarning,SamplerID,Status)
    VALUES (@SampleID,@OrderID,@RegulationID,@Barcode,@SamplingLocation,@SamplingTime,
        @FieldTemperature,@FieldHumidity,@WeatherCondition,@FieldImage,@IsWarning,@SamplerID,@Status);
END;
GO
CREATE PROCEDURE dbo.sp_UpdateSample
    @SampleID VARCHAR(50), @SamplingLocation NVARCHAR(255) = NULL, @SamplingTime DATETIME = NULL,
    @FieldTemperature FLOAT = NULL, @FieldHumidity FLOAT = NULL, @WeatherCondition NVARCHAR(100) = NULL,
    @FieldImage VARBINARY(MAX) = NULL, @IsWarning BIT = 0, @SamplerID VARCHAR(50) = NULL, @Status INT = 0
AS BEGIN SET NOCOUNT ON;
    UPDATE Samples SET SamplingLocation=@SamplingLocation, SamplingTime=@SamplingTime,
        FieldTemperature=@FieldTemperature, FieldHumidity=@FieldHumidity,
        WeatherCondition=@WeatherCondition, FieldImage=@FieldImage,
        IsWarning=@IsWarning, SamplerID=@SamplerID, Status=@Status
    WHERE SampleID=@SampleID;
END;
GO
CREATE PROCEDURE dbo.sp_DeleteSample @SampleID VARCHAR(50) AS
BEGIN SET NOCOUNT ON; DELETE FROM Samples WHERE SampleID=@SampleID; END;
GO
CREATE PROCEDURE dbo.sp_GetSampleById @SampleID VARCHAR(50) AS
BEGIN SET NOCOUNT ON; SELECT * FROM Samples WHERE SampleID=@SampleID; END;
GO
CREATE PROCEDURE dbo.sp_GetAllSamples AS
BEGIN SET NOCOUNT ON; SELECT * FROM Samples; END;
GO
CREATE PROCEDURE dbo.sp_GetSamplesByOrder @OrderID VARCHAR(50) AS
BEGIN SET NOCOUNT ON; SELECT * FROM Samples WHERE OrderID=@OrderID; END;
GO
PRINT '  [OK] Sample CRUD (6 SPs)';
GO

CREATE PROCEDURE dbo.sp_DeleteTestResult @ResultID VARCHAR(50) AS
BEGIN SET NOCOUNT ON; DELETE FROM TestResults WHERE ResultID=@ResultID; END;
GO
CREATE PROCEDURE dbo.sp_GetTestResultById @ResultID VARCHAR(50) AS
BEGIN SET NOCOUNT ON; SELECT * FROM TestResults WHERE ResultID=@ResultID; END;
GO
CREATE PROCEDURE dbo.sp_GetTestResultsBySample @SampleID VARCHAR(50) AS
BEGIN SET NOCOUNT ON; SELECT * FROM TestResults WHERE SampleID=@SampleID; END;
GO
PRINT '  [OK] TestResult Read/Delete (3 SPs)';
GO

CREATE PROCEDURE dbo.sp_GetUserById @UserID VARCHAR(50) AS
BEGIN SET NOCOUNT ON;
    SELECT UserID, Username, PasswordHash, FullName, Email, AvatarData, FaceIDData,
           IsFaceIDRegistered, RoleID, IsActive, CreatedDate, Address, Phone, DateOfBirth, Department, EmployeeCode
    FROM Users WHERE UserID=@UserID AND IsActive=1;
END;
GO
CREATE PROCEDURE dbo.sp_GetUserByEmail @Email VARCHAR(100) AS
BEGIN SET NOCOUNT ON;
    SELECT UserID, Username, FullName, Email, IsActive FROM Users WHERE Email=@Email AND IsActive=1;
END;
GO
CREATE PROCEDURE dbo.sp_UpdatePassword @Username VARCHAR(50), @PasswordHash VARCHAR(255) AS
BEGIN SET NOCOUNT ON; UPDATE Users SET PasswordHash=@PasswordHash WHERE Username=@Username; END;
GO
CREATE PROCEDURE dbo.sp_UpdatePasswordByEmail @Email VARCHAR(100), @PasswordHash VARCHAR(255) AS
BEGIN SET NOCOUNT ON; UPDATE Users SET PasswordHash=@PasswordHash WHERE Email=@Email; END;
GO
CREATE PROCEDURE dbo.sp_UpdateProfile
    @UserID VARCHAR(50), @FullName NVARCHAR(100), @Email VARCHAR(100),
    @Phone VARCHAR(20) = NULL, @Address NVARCHAR(255) = NULL,
    @DateOfBirth DATETIME = NULL, @Department NVARCHAR(100) = NULL
AS BEGIN SET NOCOUNT ON;
    UPDATE Users SET FullName=@FullName, Email=@Email, Phone=@Phone,
        Address=@Address, DateOfBirth=@DateOfBirth, Department=@Department
    WHERE UserID=@UserID;
END;
GO
CREATE PROCEDURE dbo.sp_UpdateAvatar @UserID VARCHAR(50), @AvatarData VARBINARY(MAX) AS
BEGIN SET NOCOUNT ON; UPDATE Users SET AvatarData=@AvatarData WHERE UserID=@UserID; END;
GO
CREATE PROCEDURE dbo.sp_UpdateEmployee
    @UserID VARCHAR(50), @Username VARCHAR(50), @PasswordHash VARCHAR(255),
    @FullName NVARCHAR(100), @Email VARCHAR(100), @RoleID VARCHAR(50), @IsActive BIT,
    @Phone VARCHAR(20) = NULL, @Address NVARCHAR(255) = NULL, @Department NVARCHAR(100) = NULL,
    @DateOfBirth DATETIME = NULL, @EmployeeCode VARCHAR(20) = NULL, @AvatarData VARBINARY(MAX) = NULL
AS BEGIN SET NOCOUNT ON;
    UPDATE Users SET Username=@Username, PasswordHash=@PasswordHash, FullName=@FullName, Email=@Email,
        RoleID=@RoleID, IsActive=@IsActive, Phone=@Phone, Address=@Address, Department=@Department,
        DateOfBirth=@DateOfBirth, EmployeeCode=@EmployeeCode, AvatarData=@AvatarData
    WHERE UserID=@UserID;
END;
GO
CREATE PROCEDURE dbo.sp_DeleteEmployee @UserID VARCHAR(50) AS
BEGIN SET NOCOUNT ON; DELETE FROM Users WHERE UserID=@UserID; END;
GO
CREATE PROCEDURE dbo.sp_ToggleEmployeeActive @UserID VARCHAR(50), @IsActive BIT AS
BEGIN SET NOCOUNT ON; UPDATE Users SET IsActive=@IsActive WHERE UserID=@UserID; END;
GO
CREATE PROCEDURE dbo.sp_GetEmployeeById @UserID VARCHAR(50) AS
BEGIN SET NOCOUNT ON; SELECT * FROM Users WHERE UserID=@UserID; END;
GO
CREATE PROCEDURE dbo.sp_GetEmployeeByEmail @Email VARCHAR(100) AS
BEGIN SET NOCOUNT ON; SELECT * FROM Users WHERE Email=@Email; END;
GO
PRINT '  [OK] User/Employee CRUD (11 SPs)';
GO

CREATE PROCEDURE dbo.sp_GetAllParameters AS
BEGIN SET NOCOUNT ON; SELECT * FROM TestParameters; END;
GO
CREATE PROCEDURE dbo.sp_GetParametersByEnvironment @EnvironmentType NVARCHAR(50) AS
BEGIN SET NOCOUNT ON;
    SELECT tp.ParamID, rl.RegulationID, tp.ParamName, tp.Unit,
           CASE WHEN tp.IsField=1 THEN N'Hiện trường' ELSE N'Thí nghiệm' END AS Department,
           CASE WHEN rl.MinValue IS NOT NULL AND rl.MaxValue IS NOT NULL
                    THEN CAST(rl.MinValue AS NVARCHAR)+' - '+CAST(rl.MaxValue AS NVARCHAR)
                WHEN rl.MaxValue IS NOT NULL THEN N'≤ '+CAST(rl.MaxValue AS NVARCHAR)
                WHEN rl.MinValue IS NOT NULL THEN N'≥ '+CAST(rl.MinValue AS NVARCHAR)
                ELSE '' END AS QcvnLimit
    FROM TestParameters tp
    LEFT JOIN RegulationLimits rl ON tp.ParamID=rl.ParamID
    LEFT JOIN Regulations r ON rl.RegulationID=r.RegulationID
    WHERE r.EnvironmentType=@EnvironmentType OR r.EnvironmentType IS NULL
    ORDER BY tp.IsField DESC, tp.ParamName;
END;
GO
PRINT '  [OK] StandardParameter (2 SPs)';
GO

CREATE PROCEDURE dbo.sp_GetResultHistory @ResultID VARCHAR(50) AS
BEGIN SET NOCOUNT ON;
    SELECT HistoryID, ResultID, OldValue, NewValue, ChangedBy, ChangedAt
    FROM ResultHistory WHERE ResultID=@ResultID ORDER BY ChangedAt DESC;
END;
GO
PRINT '  [OK] AuditLog (1 SP)';
GO

-- ────────────────────────────────────────────────────────────
-- SP: Thêm lịch sử kết quả thủ công (dự phòng — trigger tự làm)
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_AddResultHistory
    @HistoryID VARCHAR(50),
    @ResultID  VARCHAR(50),
    @OldValue  FLOAT        = NULL,
    @NewValue  FLOAT        = NULL,
    @ChangedBy VARCHAR(50)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO ResultHistory (HistoryID, ResultID, OldValue, NewValue, ChangedBy, ChangedAt)
    VALUES (ISNULL(@HistoryID, NEWID()), @ResultID, @OldValue, @NewValue, @ChangedBy, GETDATE());
END;
GO
PRINT '  [OK] sp_AddResultHistory';
GO

-- ────────────────────────────────────────────────────────────
-- SP: Đếm số kết quả còn thiếu trong 1 Order (PDF readiness check)
-- Dùng bởi TestingResultRepository.IsOrderResultCompleteAsync()
-- ────────────────────────────────────────────────────────────
CREATE PROCEDURE dbo.sp_GetPendingResultCount
    @OrderID VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    -- Đếm số SamplingPlanItem chưa có TestResult tương ứng
    SELECT COUNT(*) AS PendingCount
    FROM SamplingPlanItems spi
    WHERE spi.OrderID = @OrderID
      AND NOT EXISTS (
          SELECT 1
          FROM TestResults tr
          INNER JOIN Samples s ON tr.SampleID = s.SampleID
          WHERE s.OrderID  = @OrderID
            AND tr.ParamID = spi.ParamID
      );
END;
GO
PRINT '  [OK] sp_GetPendingResultCount';
GO

PRINT '';
PRINT '============================================================';
PRINT '  ALL PROGRAMMABILITY OBJECTS CREATED SUCCESSFULLY';
PRINT '============================================================';
PRINT 'TỔNG CỘNG: 6 triggers + 6 functions + 62 stored procedures (bao gồm sp_AddAuditLog, sp_GetAuditLogs trong Init.sql)';
GO

PRINT '';
PRINT '============================================================';
PRINT '  ALL PROGRAMMABILITY OBJECTS CREATED SUCCESSFULLY';
PRINT '============================================================';
PRINT '';
PRINT 'TRIGGERS (6):';
PRINT '  1. trg_TestResults_AutoWarning    — Auto IsWarning qua RegulationLimits';
PRINT '  2. trg_TestResults_AuditHistory   — Ghi ResultHistory + ChangeReason';
PRINT '  3. trg_Users_UpdatedAt            — Auto timestamp';
PRINT '  4. trg_Contracts_UpdatedAt        — Auto timestamp';
PRINT '  5. trg_Orders_UpdatedAt           — Auto timestamp';
PRINT '  6. trg_Samples_UpdatedAt          — Auto timestamp';
PRINT '';
PRINT 'SCALAR FUNCTIONS (4):';
PRINT '  1. fn_IsResultExceedingLimit      — Kiểm tra vượt ngưỡng QCVN';
PRINT '  2. fn_GetContractStatus           — Tính status theo ngày';
PRINT '  3. fn_GetOrderCompletionPercent   — % hoàn thành Order';
PRINT '  4. fn_FormatContractID            — Format HD-XXXX';
PRINT '';
PRINT 'TABLE-VALUED FUNCTIONS (2):';
PRINT '  5. fn_GetParametersByRegulation   — Thông số theo QCVN';
PRINT '  6. fn_GetTestResultsWithLimits    — Kết quả kèm ngưỡng QCVN';
PRINT '';
PRINT 'STORED PROCEDURES — CORE BUSINESS (6):';
PRINT '  1. sp_SaveTestResult              — Lưu kết quả (INSERT/UPDATE)';
PRINT '  2. sp_GenerateEmployeeCode        — Sinh mã nhân viên (atomic)';
PRINT '  3. sp_UpsertEmployee              — UPSERT nhân viên';
PRINT '  4. sp_DeleteSamplingArea          — Cascade delete khu vực (atomic)';
PRINT '  5. sp_SaveSamplingPlan            — Lưu kế hoạch từ JSON (atomic)';
PRINT '  6. sp_RegisterFaceID              — Đăng ký FaceID (atomic)';
PRINT '';
PRINT 'STORED PROCEDURES — QUERIES & REPORTS (13):';
PRINT '  7.  sp_GetDashboardStats          — Tổng quan GĐ (4 result sets)';
PRINT '  8.  sp_GetContractCards           — DS hợp đồng';
PRINT '  9.  sp_SearchContracts            — Tìm kiếm HĐ';
PRINT '  10. sp_GetExpiringContracts       — HĐ sắp/đã hết hạn';
PRINT '  11. sp_UpdateContractStatuses     — Batch cập nhật (Hangfire)';
PRINT '  12. sp_GetOrdersByContract        — Orders theo HĐ';
PRINT '  13. sp_GetTestResultsByOrder      — Kết quả theo Order';
PRINT '  14. sp_GetSamplingPlanByOrder     — Kế hoạch theo Order';
PRINT '  15. sp_GetWarningResults          — Kết quả vượt ngưỡng';
PRINT '  16. sp_GetRevenueByQuarter        — Doanh thu theo quý';
PRINT '  17. sp_GetEmployeeList            — DS nhân viên';
PRINT '  18. sp_AuthenticateUser           — Xác thực đăng nhập';
PRINT '  19. sp_GetContractReport          — Báo cáo tổng hợp HĐ';
PRINT '';
PRINT 'TỔNG CỘNG: 6 triggers + 6 functions + 62 stored procedures tot';
GO
