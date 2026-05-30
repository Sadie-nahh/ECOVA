USE ECOVA;
GO
SET NOCOUNT ON;
GO

-- ============================================================
-- XÓA DỮ LIỆU CŨ theo thứ tự ngược FK
-- ============================================================
PRINT 'Cleaning old data...';
GO

DELETE FROM AuditLogs        WHERE 1=1;
DELETE FROM ResultHistory     WHERE 1=1;
DELETE FROM TestResults       WHERE 1=1;
DELETE FROM SamplingPlanItems WHERE 1=1;
DELETE FROM Samples           WHERE 1=1;
DELETE FROM Orders            WHERE 1=1;
DELETE FROM CustomerFeedbacks WHERE 1=1;
DELETE FROM RegulationLimits  WHERE 1=1;
DELETE FROM TestParameters    WHERE 1=1;
DELETE FROM Regulations       WHERE 1=1;
DELETE FROM Contracts         WHERE 1=1;
DELETE FROM Users             WHERE 1=1;
DELETE FROM Customers         WHERE 1=1;
DELETE FROM Roles             WHERE 1=1;
GO

-- ============================================================
-- 1. ROLES — 7 vai trò
-- ============================================================
PRINT 'Seeding Roles...';
GO
INSERT INTO Roles (RoleID, RoleCode, RoleName, Description) VALUES
('R01', 'ADMIN',     N'Quản trị Hệ thống',   N'Toàn quyền trên hệ thống'),
('R02', 'DIRECTOR',  N'Giám đốc',              N'Xem tổng quan & phê duyệt'),
('R03', 'SALES',     N'Phòng Kinh doanh',     N'Quản lý khách hàng và hợp đồng'),
('R04', 'FIELD_QA',  N'Phòng Hiện trường',    N'Lấy mẫu và nhập kết quả đo hiện trường'),
('R05', 'LAB_QA',    N'Phòng Thí nghiệm',    N'Phân tích mẫu và nhập kết quả thí nghiệm'),
('R06', 'PLANNING',  N'Phòng Kế hoạch',       N'Lập kế hoạch quan trắc và phân công'),
('R07', 'RESULTS',   N'Phòng Kết quả',        N'Phê duyệt và quản lý kết quả quan trắc');
GO

-- ============================================================
-- 2. USERS — 12 tài khoản (Password: "admin" — BCrypt hash)
-- ============================================================
PRINT 'Seeding Users...';
GO
INSERT INTO Users (UserID, EmployeeCode, Username, PasswordHash, FullName, Email, Phone, Department, RoleID, IsActive)
VALUES
-- R01: Quản trị hệ thống
('U01', 'EMP001', 'admin',
 '$2a$11$8I9SkIq8cu4l997cVvu/9u8H7PuJOda/18i8Cm63wfw3zvD6VbrBG',
 N'Nguyễn Trung Nguyên',   'trnguynnn@gmail.com',        '0777616202', N'IT',               'R01', 1),

-- R02: Giám đốc
('U02', 'EMP002', 'director',
 '$2a$11$8I9SkIq8cu4l997cVvu/9u8H7PuJOda/18i8Cm63wfw3zvD6VbrBG',
 N'Phạm Ngọc Anh',         'pna.ngocanhpham@gmail.com',  '0987654321', N'Ban Giám đốc',     'R02', 1),

-- R03: Phòng Kinh doanh (2 nhân viên)
('U03', 'EMP003', 'tuyethan',
 '$2a$11$8I9SkIq8cu4l997cVvu/9u8H7PuJOda/18i8Cm63wfw3zvD6VbrBG',
 N'Hồ Thị Tuyết Hân',      'tuyethantt1@gmail.com',      '0901234789', N'Kinh doanh',       'R03', 1),

('U06', 'EMP006', 'minhkhoa',
 '$2a$11$8I9SkIq8cu4l997cVvu/9u8H7PuJOda/18i8Cm63wfw3zvD6VbrBG',
 N'Trần Minh Khoa',         'minhkhoa@ecova.com',         '0901234700', N'Kinh doanh',       'R03', 1),

-- R04: Phòng Hiện trường (2 nhân viên)
('U04', 'EMP004', 'baolongnv',
 '$2a$11$8I9SkIq8cu4l997cVvu/9u8H7PuJOda/18i8Cm63wfw3zvD6VbrBG',
 N'Lương Bảo Long',         'baolong2k6iter@gmail.com',   '0933445566', N'Hiện trường',      'R04', 1),

('U10', 'EMP010', 'duchuy',
 '$2a$11$8I9SkIq8cu4l997cVvu/9u8H7PuJOda/18i8Cm63wfw3zvD6VbrBG',
 N'Lê Đức Huy',             'leduchuy29122005@gmail.com', '0933445567', N'Hiện trường',      'R04', 1),

-- R05: Phòng Thí nghiệm (2 nhân viên)
('U05', 'EMP005', 'thuha',
 '$2a$11$8I9SkIq8cu4l997cVvu/9u8H7PuJOda/18i8Cm63wfw3zvD6VbrBG',
 N'Nguyễn Thị Thu Hà',     'thuha.lab@ecova.com',         '0944556677', N'Thí nghiệm',       'R05', 1),

('U07', 'EMP007', 'anhtuan',
 '$2a$11$8I9SkIq8cu4l997cVvu/9u8H7PuJOda/18i8Cm63wfw3zvD6VbrBG',
 N'Trần Anh Tuấn',          'anhtuan.lab@ecova.com',       '0944556688', N'Thí nghiệm',       'R05', 1),

-- R06: Phòng Kế hoạch (2 nhân viên)
('U08', 'EMP008', 'huonggiang',
 '$2a$11$8I9SkIq8cu4l997cVvu/9u8H7PuJOda/18i8Cm63wfw3zvD6VbrBG',
 N'Đỗ Thị Hương Giang',    'huonggiang.plan@ecova.com',   '0955667788', N'Kế hoạch',         'R06', 1),

('U11', 'EMP011', 'minhtung',
 '$2a$11$8I9SkIq8cu4l997cVvu/9u8H7PuJOda/18i8Cm63wfw3zvD6VbrBG',
 N'Võ Minh Tùng',           'minhtung.plan@ecova.com',    '0955667799', N'Kế hoạch',         'R06', 1),

-- R07: Phòng Kết quả (2 nhân viên)
('U09', 'EMP009', 'thanhloannv',
 '$2a$11$8I9SkIq8cu4l997cVvu/9u8H7PuJOda/18i8Cm63wfw3zvD6VbrBG',
 N'Bùi Thị Thanh Loan',    'thanhloannv@ecova.com',       '0966778899', N'Kết quả',          'R07', 1),

('U12', 'EMP012', 'hoangnam',
 '$2a$11$8I9SkIq8cu4l997cVvu/9u8H7PuJOda/18i8Cm63wfw3zvD6VbrBG',
 N'Nguyễn Hoàng Nam',       'hoangnam.result@ecova.com',  '0966778800', N'Kết quả',          'R07', 1);
GO

-- ============================================================
-- 3. KHÁCH HÀNG — 10 doanh nghiệp đa ngành
-- ============================================================
PRINT 'Seeding Customers...';
GO
INSERT INTO Customers (CustomerID, TaxCode, CompanyName, Address, Representative, ContactEmail, PhoneNumber) VALUES
('C01', '0101234567', N'Công ty CP Bao Bì Xanh',        N'Lô A12, KCN Tân Bình, TP.HCM',              N'Nguyễn Văn An',    'contact@baobixanh.vn',       '0901234567'),
('C02', '0301234567', N'Tập đoàn VinaSteel',             N'Lô B5, KCN Phú Mỹ, Bà Rịa-Vũng Tàu',       N'Trần Thị Bình',    'info@vinasteel.vn',          '0912345678'),
('C03', '0401234567', N'Nhà máy Sữa Vinalong',           N'Lô C7, KCN Biên Hòa, Đồng Nai',             N'Lê Văn Cường',     'admin@vinalong.vn',          '0923456789'),
('C04', '0501234567', N'Dệt may Thái Bình',              N'Lô D2, KCN Đức Hòa, Long An',               N'Phạm Thị Diệu',   'info@textile-tb.com',        '0934567890'),
('C05', '0601234567', N'Hóa chất Hiệp Phước',            N'Lô E9, KCN Hiệp Phước, Nhà Bè, TP.HCM',    N'Hoàng Văn Em',     'contact@chemicalhp.com',     '0945678901'),
('C06', '0701234567', N'Trung Nguyên Legend',             N'Số 82 Trần Hưng Đạo, Q1, TP.HCM',           N'Đặng Lê Nguyên Vũ', 'info@trungnguyen.com.vn',  '0956789012'),
('C07', '0801234567', N'Vinamilk',                        N'Lô F3, KCN Mỹ Phước, Bình Dương',           N'Mai Kiều Liên',    'contact@vinamilk.com.vn',    '0967890123'),
('C08', '0901234567', N'Hòa Phát Group',                  N'KCN Phố Nối A, Hưng Yên',                   N'Trần Đình Long',   'info@hoaphat.com.vn',        '0978901234'),
('C09', '1001234567', N'Tập đoàn Masan',                  N'Lô G1, KCN Hải Dương',                      N'Nguyễn Đăng Quang','info@masangroup.com',        '0989012345'),
('C10', '1101234567', N'Nhựa Bình Minh',                  N'Lô H4, KCN Sóng Thần, Bình Dương',          N'Trần Bảo Sơn',    'contact@binhminhplastic.com','0990123456');
GO

-- ============================================================
-- 4. QCVN REGULATIONS — 3 nền mẫu (cập nhật mới nhất)
-- ============================================================
PRINT 'Seeding Regulations...';
GO
INSERT INTO Regulations (RegulationID, Code, Name, EnvironmentType) VALUES
-- KCN xả thải → QCVN 40 phù hợp nhất (nước thải công nghiệp)
('RG01', 'QCVN 40:2011/BTNMT',    N'Quy chuẩn nước thải công nghiệp',  N'Nước thải'),
-- Cập nhật phiên bản mới nhất (2023 thay thế 2013)
('RG02', 'QCVN 05:2023/BTNMT',    N'Quy chuẩn chất lượng không khí', N'Không khí'),
('RG03', 'QCVN 03-MT:2015/BTNMT', N'Quy chuẩn chất lượng đất',      N'Đất');
GO

-- ============================================================
-- 5. THÔNG SỐ ĐO — 24 thông số (đủ 3 nền mẫu, Hiện trường + PTN)
-- ============================================================
PRINT 'Seeding TestParameters...';
GO
-- ======= KHÔNG KHÍ — Hiện trường (IsField=1) =======
-- Các thông số vi khí hậu, đo trực tiếp tại hiện trường
INSERT INTO TestParameters (ParamID, ParamName, Unit, TestMethod, IsField, Price) VALUES
('P01', N'Ánh sáng',                   'lx',    'TCVN 5176:1990',    1,  50000),
('P02', N'Nhiệt độ không khí',        N'°C',   'QCVN 26:2016/BYT',  1,  50000),
('P03', N'Độ ẩm tương đối',           '%',     'QCVN 26:2016/BYT',  1,  50000),
('P04', N'Tốc độ gió',                 'm/s',   'TCVN 4527:1988',    1,  80000),
('P05', N'Tiếng ồn tương đương',     'dB(A)', 'QCVN 24:2016/BYT',  1, 100000);

-- ======= KHÔNG KHÍ — Thí nghiệm (IsField=0) =======
-- Thông số theo QCVN 05:2023/BTNMT (đo trung bình 24 giờ)
INSERT INTO TestParameters (ParamID, ParamName, Unit, TestMethod, IsField, Price) VALUES
('P06', N'Tổng bụi lơ lửng (TSP)',    N'µg/m³', 'TCVN 5067:1995',   0, 300000),
('P07', N'Nitơ điôxit (NO₂)',          N'µg/m³', 'TCVN 6138:1996',   0, 300000),
('P08', N'Lưu huỳnh điôxit (SO₂)',   N'µg/m³', 'TCVN 5971:1995',   0, 300000),
('P09', N'Carbon monoxide (CO)',     N'µg/m³', 'TCVN 5972:1995',   0, 250000),
('P10', N'Ozone (O₃)',                N'µg/m³', 'TCVN 7171:2002',   0, 350000);

-- ======= NƯỚC THẢI — Hiện trường (IsField=1) =======
INSERT INTO TestParameters (ParamID, ParamName, Unit, TestMethod, IsField, Price) VALUES
('P11', 'pH',                         '-',     'TCVN 6492:2011',    1,  50000),
('P12', N'Nhiệt độ nước',             N'°C',   'SMEWW 2550B',       1,  50000);

-- ======= NƯỚC THẢI — Thí nghiệm (IsField=0) =======
-- Theo QCVN 40:2011/BTNMT — nước thải công nghiệp, áp dụng Cột B
INSERT INTO TestParameters (ParamID, ParamName, Unit, TestMethod, IsField, Price) VALUES
('P13', N'BOD₅ (20°C)',                'mg/L',  'TCVN 6001-1:2008',  0, 250000),
('P14', 'COD',                        'mg/L',  'SMEWW 5220C',       0, 200000),
('P15', N'Chất rắn lơ lửng (TSS)',    'mg/L',  'TCVN 6625:2000',    0, 150000),
('P16', N'Tổng Nitơ (TN)',             'mg/L',  'TCVN 6638:2000',    0, 200000),
('P17', N'Tổng Phospho (TP)',           'mg/L',  'SMEWW 4500-P-E',    0, 200000),
('P18', N'Amoni (NH₄⁺-N)',              'mg/L',  'SMEWW 4500-NH3-F',  0, 180000);

-- ======= ĐẤT — Hiện trường (IsField=1) =======
INSERT INTO TestParameters (ParamID, ParamName, Unit, TestMethod, IsField, Price) VALUES
('P19', N'pH đất',                     '-',     'TCVN 5979:2007',    1,  80000);

-- ======= ĐẤT — Thí nghiệm (IsField=0) =======
-- Kim loại nặng theo QCVN 03-MT:2015/BTNMT — đất khu công nghiệp
INSERT INTO TestParameters (ParamID, ParamName, Unit, TestMethod, IsField, Price) VALUES
('P20', N'Chì (Pb)',               'mg/kg', 'TCVN 6496:2009',    0, 350000),
('P21', N'Kẽm (Zn)',               'mg/kg', 'TCVN 6496:2009',    0, 350000),
('P22', N'Cadimi (Cd)',             'mg/kg', 'TCVN 6496:2009',    0, 400000),
('P23', N'Asen (As)',               'mg/kg', 'TCVN 6182:2008',    0, 450000),
('P24', N'Đồng (Cu)',              'mg/kg', 'TCVN 6496:2009',    0, 400000);
GO

-- ============================================================
-- 6. GIỚI HẠN QCVN — chính xác theo tiêu chuẩn hiện hành
-- ============================================================
PRINT 'Seeding RegulationLimits...';
GO

-- ── QCVN 05:2023 — Không khí (Hiện trường: vi khí hậu)
-- Các thông số hiện trường không có trong QCVN 05 ngoài trời
-- nhưng cần có entry để GetParametersByOrderIdAsync JOIN đúng
INSERT INTO RegulationLimits (LimitID, RegulationID, ParamID, MinValue, MaxValue) VALUES
('L01', 'RG02', 'P01', 300,  500),  -- Ánh sáng   : 300–500 lx  (QCVN 22/BYT về MT lao động)
('L02', 'RG02', 'P02',  18,   38),  -- Nhiệt độ KK: 18–38°C     (QCVN 26:2016/BYT)
('L03', 'RG02', 'P03',  40,   80),  -- Độ ẩm      : 40–80%      (QCVN 26:2016/BYT)
('L04', 'RG02', 'P04', NULL,   2),  -- Tốc độ gió: ≤ 2 m/s     (QCVN 26:2016/BYT)
('L05', 'RG02', 'P05', NULL,  85);  -- Tiếng ồn  : ≤ 85 dB(A)  (QCVN 24:2016/BYT, làm việc 8h)

-- ── QCVN 05:2023 — Không khí (Thí nghiệm: trung bình 24 giờ)
-- Nguồn: Bảng 1 QCVN 05:2023/BTNMT
INSERT INTO RegulationLimits (LimitID, RegulationID, ParamID, MinValue, MaxValue) VALUES
('L06', 'RG02', 'P06', NULL,  200), -- TSP  ≤ 200 µg/m³  (TB 24h, QCVN 05:2023)
('L07', 'RG02', 'P07', NULL,  100), -- NO₂  ≤ 100 µg/m³  (TB 24h; 200 là TB 1h)
('L08', 'RG02', 'P08', NULL,  125), -- SO₂  ≤ 125 µg/m³  (TB 24h; 350 là TB 1h)
('L09', 'RG02', 'P09', NULL,10000), -- CO   ≤ 10.000 µg/m³(TB 8h; 30.000 là TB 1h)
('L10', 'RG02', 'P10', NULL,  120); -- O₃   ≤ 120 µg/m³   (TB 8h;  200 là TB 1h)

-- ── QCVN 40:2011/BTNMT — Nước thải công nghiệp, CỘT B
-- Cột B áp dụng khi xả vào nguồn nước không dùng cấp nước SH
-- Phù hợp với các KCN Tân Bình, Phú Mỹ, Biên Hòa...
INSERT INTO RegulationLimits (LimitID, RegulationID, ParamID, MinValue, MaxValue) VALUES
('L11', 'RG01', 'P11',  5.5,   9.0), -- pH        : 5,5–9,0      (QCVN 40 Cột B)
('L12', 'RG01', 'P12', NULL,  40.0), -- Nhiệt độ  : ≤ 40°C       (QCVN 40 Cột B)
('L13', 'RG01', 'P13', NULL,  50.0), -- BOD₅      : ≤ 50 mg/L    (QCVN 40 Cột B)
('L14', 'RG01', 'P14', NULL, 150.0), -- COD       : ≤ 150 mg/L   (QCVN 40 Cột B)
('L15', 'RG01', 'P15', NULL, 100.0), -- TSS       : ≤ 100 mg/L   (QCVN 40 Cột B)
('L16', 'RG01', 'P16', NULL,  40.0), -- TN        : ≤ 40 mg/L    (QCVN 40 Cột B)
('L17', 'RG01', 'P17', NULL,   6.0), -- TP        : ≤ 6 mg/L     (QCVN 40 Cột B)
('L18', 'RG01', 'P18', NULL,  10.0); -- NH₄⁺-N    : ≤ 10 mg/L    (QCVN 40 Cột B)

-- ── QCVN 03-MT:2015/BTNMT — Kim loại nặng trong đất KHU CÔNG NGHIỆP
-- Áp dụng: đất khu công nghiệp / thương mại - dịch vụ
-- Nguồn: Bảng 2, QCVN 03-MT:2015/BTNMT
INSERT INTO RegulationLimits (LimitID, RegulationID, ParamID, MinValue, MaxValue) VALUES
('L19', 'RG03', 'P19',  5.0,  8.5), -- pH đất    : 5,0–8,5       (tham chiếu TCVN 5979)
('L20', 'RG03', 'P20', NULL, 300),   -- Pb (Chì)  : ≤ 300 mg/kg   (QCVN 03-MT:2015, đất CN)
('L21', 'RG03', 'P21', NULL, 300),   -- Zn (Kẽm)  : ≤ 300 mg/kg   (QCVN 03-MT:2015, đất CN)
('L22', 'RG03', 'P22', NULL,  10),   -- Cd (Cadimi): ≤ 10  mg/kg   (QCVN 03-MT:2015, đất CN)
('L23', 'RG03', 'P23', NULL,  25),   -- As (Asen)  : ≤ 25  mg/kg   (QCVN 03-MT:2015, đất CN)
('L24', 'RG03', 'P24', NULL, 300);   -- Cu (Đồng)  : ≤ 300 mg/kg   (QCVN 03-MT:2015, đất CN)
GO

-- ============================================================
-- 7. HOP DONG — 40 HD, TAT CA trong 4 Quy hien thi (rolling 12 thang)
--    Q3/2025 (Jul-Sep): 7 HD (HD-0034..0040)  — Expired (bieu do cu nhat)
--    Q4/2025 (Oct-Dec): 10 HD (HD-0024..0033) — Expiring (tang vua)
--    Q1/2026 (Jan-Mar): 14 HD (HD-0010..0023) — peak, half Active (tang manh)
--    Q2/2026 (Apr):      9 HD (HD-0001..0009) — mostly Active (quy hien tai)
--
--    Bieu do "Hop dong moi":  7 → 10 → 14 → 9  (tang den peak Q1, giam nhe Q2)
--    Bieu do "Hoan thanh":    7 → 10 →  7 →  1  (giam dan - HDs cu da xong het)
--    AI Preview: Top3 High = Vinamilk/Vinalong/HoaChatHP, Top3 Low = HoaPhat/DetMay/VinaSteel
--    NOTIFICATION: HD-0020..0023 ValidTo = +10..25 ngay → bat thong bao!
-- ============================================================
PRINT 'Seeding Contracts...';
GO
DECLARE @i   INT = 1;
DECLARE @sd  DATETIME;
DECLARE @vt  DATETIME;
DECLARE @cid VARCHAR(4);

WHILE @i <= 40
BEGIN
    -- CustomerID: C01-C10, moi KH co dung 4 HD phan bo deu (10×4=40)
    SET @cid = 'C' + RIGHT('0' + CAST(CASE WHEN @i % 10 = 0 THEN 10 ELSE @i % 10 END AS VARCHAR), 2);

    -- SignedDate: phan bo chinh xac vao tung quy
    SET @sd = CASE
        WHEN @i <=  9 THEN DATEADD(DAY, -@i,                     GETDATE())  -- Q2/2026: -1 to  -9 ngay
        WHEN @i <= 23 THEN DATEADD(DAY, -(15  + (@i-10) *  6),   GETDATE())  -- Q1/2026: -15 to -93 ngay
        WHEN @i <= 33 THEN DATEADD(DAY, -(105 + (@i-24) *  8),   GETDATE())  -- Q4/2025: -105 to -177 ngay
        ELSE               DATEADD(DAY, -(195 + (@i-34) * 12),   GETDATE())  -- Q3/2025: -195 to -267 ngay
    END;

    -- ValidTo: HD-0020..0023 het han trong 10-25 ngay → triggering notifications!
    SET @vt = CASE
        WHEN @i IN (20,21,22,23) THEN DATEADD(DAY, 10 + (@i-20)*5, GETDATE())
        WHEN @i <=  9            THEN DATEADD(MONTH, 12 + @i % 6,  @sd)   -- Q2/26: +12-17 thang
        WHEN @i <= 23            THEN DATEADD(MONTH,  4 + @i % 5,  @sd)   -- Q1/26: +4-8 thang
        WHEN @i <= 33            THEN DATEADD(MONTH, 10 + @i % 7,  @sd)   -- Q4/25: +10-16 thang
        ELSE                          DATEADD(MONTH, 18 - (@i-34)*2, @sd) -- Q3/25: +4-18 thang
    END;

    INSERT INTO Contracts
        (ContractID, CustomerID, SignedDate, ValidFrom, ValidTo,
         ContractFilePath, Status, CreatedBy, TotalContractValue, IndustryType, RenewalLabel)
    VALUES (
        'HD-' + RIGHT('0000' + CAST(@i AS VARCHAR), 4),
        @cid, @sd, @sd, @vt, NULL,
        -- Status: thiet ke de tao 2 duong bieu do dep va chinh xac
        --  Q2/26: New=9, Done=1   │  Q1/26: New=14, Done=7
        --  Q4/25: New=10, Done=10 │  Q3/25: New=7,  Done=7
        CASE
            WHEN @i <=  9 AND @i = 5         THEN 2  -- Q2/26: chi HD-0005 Expiring
            WHEN @i <=  9                    THEN 0  -- Q2/26: 8 Active
            WHEN @i IN (20,21,22,23)         THEN 2  -- Q1/26: het han sap (4 HD)
            WHEN @i <= 19 AND @i % 4 = 2    THEN 2  -- Q1/26: them 3 Expiring (i=10,14,18)
            WHEN @i <= 23                    THEN 0  -- Q1/26: 7 Active
            WHEN @i <= 33 AND @i % 5 = 0    THEN 3  -- Q4/25: 2 Expired (i=25,30)
            WHEN @i <= 33                    THEN 2  -- Q4/25: 8 Expiring
            ELSE                                  3  -- Q3/25: 7 Expired
        END,
        CASE WHEN @i % 2 = 0 THEN 'U03' ELSE 'U06' END,
        -- TotalContractValue: gan sat ngay cua tung KH thuc te
        CASE @cid
            WHEN 'C08' THEN 250000000 + (@i * 8000000)  -- Hoa Phat Steel lon
            WHEN 'C02' THEN 300000000 + (@i * 6000000)  -- VinaSteel lon
            WHEN 'C07' THEN 170000000 + (@i * 4000000)  -- Vinamilk kha lon
            WHEN 'C09' THEN 150000000 + (@i * 3500000)  -- Masan trung binh lon
            WHEN 'C05' THEN 120000000 + (@i * 2500000)  -- Hoa chat HP
            WHEN 'C10' THEN  90000000 + (@i * 2000000)  -- Nhua Binh Minh
            WHEN 'C06' THEN  65000000 + (@i * 1800000)  -- Trung Nguyen
            WHEN 'C04' THEN  50000000 + (@i * 1500000)  -- Det may Thai Binh
            WHEN 'C03' THEN  45000000 + (@i * 1200000)  -- Vinalong
            ELSE              30000000 + (@i * 1000000)  -- C01 Bao Bi Xanh
        END,
        -- IndustryType: chinh xac theo nganh nghiep cua tung KH
        CASE @cid
            WHEN 'C01' THEN N'FoodAndBeverage'
            WHEN 'C02' THEN N'Steel'
            WHEN 'C03' THEN N'FoodAndBeverage'
            WHEN 'C04' THEN N'Textile'
            WHEN 'C05' THEN N'Chemical'
            WHEN 'C06' THEN N'FoodAndBeverage'
            WHEN 'C07' THEN N'FoodAndBeverage'
            WHEN 'C08' THEN N'Steel'
            WHEN 'C09' THEN N'Manufacturing'
            ELSE             N'Manufacturing'   -- C10 Nhua Binh Minh
        END,
        -- RenewalLabel: phu hop voi du lieu CustomerFeedbacks (ResponseTime + violations)
        CASE
            WHEN @cid = 'C08'               THEN 0  -- Hoa Phat: violations=3 → khong tai ky
            WHEN @cid = 'C04' AND @i%2 = 0 THEN 0  -- Det may: 50% khong tai ky (RT cham)
            WHEN @cid = 'C02' AND @i%4 = 0 THEN 0  -- VinaSteel: 25% rui ro (violations=2)
            WHEN @cid = 'C09' AND @i%3 = 0 THEN 0  -- Masan: 33% rui ro (vi pham luc nay)
            ELSE                                 1  -- Cac KH khac: tai ky tot
        END
    );
    SET @i = @i + 1;
END;
GO


-- ============================================================
-- 8. ORDERS — 80 lenh quan trac (2 lenh/hop dong × 40 hop dong)
--    OrderDate tang dan theo 5 khoang thoi gian (~73 ngay/khoang):
--    Period 5 (< 25d ago):   @i=01..12 → 24 don (moi nhat, nhieu nhat)
--    Period 4 (25-55d ago):  @i=13..22 → 20 don
--    Period 3 (55-100d ago): @i=23..29 → 14 don
--    Period 2 (100-160d ago):@i=30..35 → 12 don
--    Period 1 (>160d ago):   @i=36..40 → 10 don (cu nhat, it nhat)
--    Pollution Risk: ~22% (chi 5 don/22 recent don la InProgress/New)
-- ============================================================
PRINT 'Seeding Orders...';
GO
DECLARE @i       INT = 1;
DECLARE @ordIdx  INT = 1;
DECLARE @ctrId   VARCHAR(50);
DECLARE @env     INT;
DECLARE @oDate   DATETIME;

WHILE @i <= 40
BEGIN
    SET @ctrId = 'HD-' + RIGHT('0000' + CAST(@i AS VARCHAR), 4);
    SET @env   = @i % 3;

    -- OrderDate: phan bo tao Activity Chart tang dan dep
    SET @oDate = DATEADD(DAY, CASE
        WHEN @i <= 12 THEN -(@i * 2 + 1)          -- -3 to -25 days (Period 5, newest)
        WHEN @i <= 22 THEN -(28 + (@i-12) * 3)    -- -28 to -58 days (Period 4)
        WHEN @i <= 29 THEN -(62 + (@i-22) * 6)    -- -62 to -104 days (Period 3)
        WHEN @i <= 35 THEN -(110 + (@i-29) * 9)   -- -110 to -164 days (Period 2)
        ELSE               -(170 + (@i-35) * 14)  -- -170 to -226 days (Period 1, oldest)
    END, GETDATE());

    -- === ORDER 1: Khu vuc chinh ===
    INSERT INTO Orders (OrderID, ContractID, OrderName, OrderDate, Deadline, Status, IsApproved, CreatedBy, EnvironmentType)
    VALUES (
        'ORD-' + RIGHT('0000' + CAST(@ordIdx AS VARCHAR), 4),
        @ctrId,
        CASE @env
            WHEN 1 THEN N'Khu vực ống khói chính'
            WHEN 2 THEN N'Cổng xả nước thải A'
            ELSE        N'Khu đất phía Bắc'
        END,
        @oDate,
        DATEADD(DAY, 15, @oDate),
        -- Status: ~22% pending trong cua so 60 ngay → gauge xanh la cay
        CASE
            WHEN @i IN (3, 8, 12)  THEN 1  -- InProgress: 3 don recent
            WHEN @i IN (5)         THEN 0  -- New: 1 don recent chua bat dau
            WHEN @i > 15 AND @i % 9 = 0 THEN 1  -- mot vai don cu bi cham tre
            ELSE                        2  -- Completed: da hoan thanh
        END,
        CASE WHEN @i IN (5) THEN 0 ELSE 1 END,
        CASE WHEN @i % 2 = 0 THEN 'U08' ELSE 'U11' END,
        CASE @env
            WHEN 1 THEN N'Không khí'
            WHEN 2 THEN N'Nước thải'
            ELSE        N'Đất'
        END
    );
    SET @ordIdx = @ordIdx + 1;

    -- === ORDER 2: Khu vuc phu ===
    INSERT INTO Orders (OrderID, ContractID, OrderName, OrderDate, Deadline, Status, IsApproved, CreatedBy, EnvironmentType)
    VALUES (
        'ORD-' + RIGHT('0000' + CAST(@ordIdx AS VARCHAR), 4),
        @ctrId,
        CASE (@env+1)%3
            WHEN 1 THEN N'Khu vực sản xuất'
            WHEN 2 THEN N'Bể lắng thứ cấp B'
            ELSE        N'Khu đất phía Nam'
        END,
        @oDate,
        DATEADD(DAY, 15, @oDate),
        CASE
            WHEN @i IN (3, 8)      THEN 1  -- InProgress: cung voi Order 1
            WHEN @i > 15 AND @i % 12 = 0 THEN 1
            ELSE                        2  -- Completed
        END,
        1,
        CASE WHEN @i % 2 = 0 THEN 'U08' ELSE 'U11' END,
        CASE (@env+1)%3
            WHEN 1 THEN N'Không khí'
            WHEN 2 THEN N'Nước thải'
            ELSE        N'Đất'
        END
    );
    SET @ordIdx = @ordIdx + 1;

    SET @i = @i + 1;
END;
GO


-- ============================================================
-- 9. SAMPLES — 80 mẫu (1 mẫu / order, chỉ cho 40 orders đầu)
-- ============================================================
PRINT 'Seeding Samples...';
GO
DECLARE @i INT = 1;
DECLARE @contractIdx INT;
DECLARE @envType INT;
DECLARE @regId VARCHAR(50);

WHILE @i <= 80
BEGIN
    SET @contractIdx = ((@i - 1) / 2) + 1;
    SET @envType = @contractIdx % 3;
    SET @regId = CASE WHEN @envType = 1 THEN 'RG02'     -- Không khí
                      WHEN @envType = 2 THEN 'RG01'     -- Nước thải
                      ELSE                    'RG03' END; -- Đất

    INSERT INTO Samples (SampleID, OrderID, RegulationID, Barcode, SamplingLocation, SamplingTime,
                         FieldTemperature, FieldHumidity, WeatherCondition, Status, SamplerID)
    VALUES (
        'SMP-' + RIGHT('000' + CAST(@i AS VARCHAR), 4),
        'ORD-' + RIGHT('000' + CAST(@i AS VARCHAR), 4),
        @regId,
        'ECO-' + CASE WHEN @envType = 1 THEN 'A' WHEN @envType = 2 THEN 'W' ELSE 'S' END
              + RIGHT('000' + CAST(@i AS VARCHAR), 4),
        -- Vị trí lấy mẫu mô tả chi tiết
        CASE
            WHEN @envType = 1 AND @i % 2 = 1 THEN N'Ống khói lò hơi chính'
            WHEN @envType = 1 AND @i % 2 = 0 THEN N'Khu vực sản xuất xưởng ' + CAST(@i % 5 + 1 AS NVARCHAR)
            WHEN @envType = 2 AND @i % 2 = 1 THEN N'Cổng xả nước thải khu ' + NCHAR(64 + (@i % 5 + 1))
            WHEN @envType = 2 AND @i % 2 = 0 THEN N'Bể lắng thứ cấp bể ' + CAST(@i % 3 + 1 AS NVARCHAR)
            WHEN @i % 2 = 1                  THEN N'Khu đất canh tác phía Bắc'
            ELSE                                   N'Khu đất công nghiệp phía Nam'
        END,
        DATEADD(DAY, -((@i * 11) % 365), GETDATE()),
        -- Nhiệt độ thực tế: 25–35°C
        25.0 + (@i % 11),
        -- Độ ẩm: 55–80%
        55.0 + (@i % 26),
        -- WeatherCondition = NULL để query GetParametersByOrderIdAsync
        -- sử dụng nhánh 'Old format' (JOIN RegulationLimits) đúng cách
        -- Code đã repurpose cột này thành ParamID storage khi Save từ Kế Hoạch
        NULL,
        -- Status: tất cả 80 mẫu hoàn thành
        2,
        -- Sampler xen kẽ U04 và U10
        CASE WHEN @i % 2 = 0 THEN 'U04' ELSE 'U10' END
    );

    SET @i = @i + 1;
END;
GO

-- ============================================================
-- 10. KẾT QUẢ PHÂN TÍCH — ~450 kết quả (đa dạng, có warning)
-- Mỗi sample có 5+ thông số tùy nền mẫu
-- ============================================================
PRINT 'Seeding TestResults...';
GO
DECLARE @i INT = 1;
DECLARE @contractIdx INT;
DECLARE @envType INT;
DECLARE @resIdx INT = 1;
DECLARE @smpId VARCHAR(50);

WHILE @i <= 80  -- Toàn bộ 80 samples (40 HĐ × 2 khu vực)
BEGIN
    SET @smpId = 'SMP-' + RIGHT('000' + CAST(@i AS VARCHAR), 4);
    SET @contractIdx = ((@i - 1) / 2) + 1;
    SET @envType = @contractIdx % 3;

    -- ===== KHÔNG KHÍ =====
    IF @envType = 1
    BEGIN
        -- Hiện trường: P01 Ánh sáng
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P01',
         300 + (@i * 7 % 200), 0, 'U04',
         DATEADD(DAY, -((@i * 11) % 365), GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P02 Nhiệt độ
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P02',
         26.0 + (@i % 10), 0, 'U04',
         DATEADD(DAY, -((@i * 11) % 365), GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P03 Độ ẩm
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P03',
         58.0 + (@i % 22), 0, 'U04',
         DATEADD(DAY, -((@i * 11) % 365), GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P04 Tốc độ gió
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P04',
         0.5 + CAST(@i % 5 AS FLOAT) * 0.8, 0, 'U04',
         DATEADD(DAY, -((@i * 11) % 365), GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P05 Tiếng ồn
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P05',
         55.0 + (@i % 30), 0, 'U04',
         DATEADD(DAY, -((@i * 11) % 365), GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- Lab: P06 TSP (vuot nguong QCVN 05:2023 TB 24h = 200 ug/m3)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P06',
         80 + (@i * 11 % 180),
         CASE WHEN 80 + (@i * 11 % 180) > 200 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 2, GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P07 NO2 (vuot nguong QCVN 05:2023 TB 24h = 100 ug/m3)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P07',
         30 + (@i * 7 % 110),
         CASE WHEN 30 + (@i * 7 % 110) > 100 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 2, GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P08 SO2 (vuot nguong QCVN 05:2023 TB 24h = 125 ug/m3)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P08',
         40 + (@i * 9 % 140),
         CASE WHEN 40 + (@i * 9 % 140) > 125 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 2, GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P09 CO (vuot nguong QCVN 05:2023 TB 8h = 10000 ug/m3)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P09',
         1500 + (@i * 19 % 11000),
         CASE WHEN 1500 + (@i * 19 % 11000) > 10000 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 2, GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P10 O3 (vuot nguong QCVN 05:2023 TB 8h = 120 ug/m3)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P10',
         30 + (@i * 11 % 130),
         CASE WHEN 30 + (@i * 11 % 130) > 120 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 2, GETDATE()));
        SET @resIdx = @resIdx + 1;
    END

    -- ===== NƯỚC THẢI =====
    ELSE IF @envType = 2
    BEGIN
        -- Hiện trường: P11 pH
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P11',
         5.5 + CAST((@i * 3 % 40) AS FLOAT) / 10.0,
         CASE WHEN (5.5 + CAST((@i * 3 % 40) AS FLOAT) / 10.0) > 9 THEN 1
              WHEN (5.5 + CAST((@i * 3 % 40) AS FLOAT) / 10.0) < 5.5 THEN 1
              ELSE 0 END,
         'U04',
         DATEADD(DAY, -((@i * 11) % 365), GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P12 Nhiệt độ nước
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P12',
         24.0 + (@i % 14),
         CASE WHEN 24.0 + (@i % 14) > 40 THEN 1 ELSE 0 END,
         'U04',
         DATEADD(DAY, -((@i * 11) % 365), GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- Lab: P13 BOD5 (vượt ngưỡng 50)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P13',
         15.0 + (@i * 3 % 55),
         CASE WHEN 15.0 + (@i * 3 % 55) > 50 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 3, GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P14 COD (vượt ngưỡng 150)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P14',
         40.0 + (@i * 7 % 160),
         CASE WHEN 40.0 + (@i * 7 % 160) > 150 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 3, GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P15 TSS (vượt ngưỡng 100)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P15',
         20.0 + (@i * 5 % 110),
         CASE WHEN 20.0 + (@i * 5 % 110) > 100 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 3, GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P16 TN (vượt ngưỡng 40)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P16',
         8.0 + (@i * 2 % 40),
         CASE WHEN 8.0 + (@i * 2 % 40) > 40 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 3, GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P17 TP (vượt ngưỡng 6)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P17',
         1.0 + CAST((@i * 3 % 80) AS FLOAT) / 10.0,
         CASE WHEN (1.0 + CAST((@i * 3 % 80) AS FLOAT) / 10.0) > 6 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 3, GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P18 NH4+ (vượt ngưỡng 10)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P18',
         1.5 + CAST((@i * 7 % 120) AS FLOAT) / 10.0,
         CASE WHEN (1.5 + CAST((@i * 7 % 120) AS FLOAT) / 10.0) > 10 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 3, GETDATE()));
        SET @resIdx = @resIdx + 1;
    END

    -- ===== ĐẤT =====
    ELSE
    BEGIN
        -- Hiện trường: P19 pH đất
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P19',
         5.0 + CAST((@i * 3 % 40) AS FLOAT) / 10.0,
         CASE WHEN (5.0 + CAST((@i * 3 % 40) AS FLOAT) / 10.0) > 8.5 THEN 1
              WHEN (5.0 + CAST((@i * 3 % 40) AS FLOAT) / 10.0) < 5 THEN 1
              ELSE 0 END,
         'U04',
         DATEADD(DAY, -((@i * 11) % 365), GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- Lab: P20 Pb (nguong dat KCN QCVN 03-MT = 300 mg/kg)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P20',
         20.0 + (@i * 11 % 310),
         CASE WHEN 20.0 + (@i * 11 % 310) > 300 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 4, GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P21 Zn (nguong dat KCN = 300 mg/kg)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P21',
         50.0 + (@i * 13 % 290),
         CASE WHEN 50.0 + (@i * 13 % 290) > 300 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 4, GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P22 Cd (nguong dat KCN = 10 mg/kg)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P22',
         0.5 + CAST((@i * 3 % 120) AS FLOAT) / 10.0,
         CASE WHEN (0.5 + CAST((@i * 3 % 120) AS FLOAT) / 10.0) > 10 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 4, GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P23 As (nguong dat KCN = 25 mg/kg)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P23',
         3.0 + CAST((@i * 7 % 260) AS FLOAT) / 10.0,
         CASE WHEN (3.0 + CAST((@i * 7 % 260) AS FLOAT) / 10.0) > 25 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 4, GETDATE()));
        SET @resIdx = @resIdx + 1;

        -- P24 Cu (nguong dat KCN = 300 mg/kg)
        INSERT INTO TestResults (ResultID, SampleID, ParamID, ResultValue, IsWarning, TesterID, EnteredAt) VALUES
        ('RES-' + RIGHT('0000' + CAST(@resIdx AS VARCHAR), 5), @smpId, 'P24',
         30.0 + (@i * 17 % 310),
         CASE WHEN 30.0 + (@i * 17 % 310) > 300 THEN 1 ELSE 0 END,
         CASE WHEN @i % 2 = 0 THEN 'U05' ELSE 'U07' END,
         DATEADD(DAY, -((@i * 11) % 365) + 4, GETDATE()));
        SET @resIdx = @resIdx + 1;
    END

    SET @i = @i + 1;
END;
GO

PRINT 'Total test results seeded: check SELECT COUNT(*) FROM TestResults';
GO

-- ============================================================
-- 10b. SAMPLING PLAN ITEMS — Phòng Kế hoạch
--      Seed 28 orders đầu (ORD-0001..ORD-0028) = 14 HĐ × 2 khu vực
-- ============================================================
PRINT 'Seeding SamplingPlanItems...';
GO
DECLARE @oi INT = 1;
DECLARE @piIdx INT = 1;
DECLARE @oId2 VARCHAR(50);
DECLARE @cIdx2 INT;
DECLARE @et2 INT;
DECLARE @reg2 VARCHAR(50);

WHILE @oi <= 28
BEGIN
    SET @oId2  = 'ORD-' + RIGHT('000' + CAST(@oi AS VARCHAR), 4);
    SET @cIdx2 = ((@oi - 1) / 2) + 1;
    SET @et2   = @cIdx2 % 3;    -- 1=Khong khi, 2=Nuoc thai, 0=Dat
    SET @reg2  = CASE WHEN @et2 = 1 THEN 'RG02' WHEN @et2 = 2 THEN 'RG01' ELSE 'RG03' END;

    IF @et2 = 1  -- KHONG KHI
    BEGIN
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P01',@reg2,N'Hiện trường',N'300–500 lx');    SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P02',@reg2,N'Hiện trường',N'18–32 °C');     SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P03',@reg2,N'Hiện trường',N'40–80 %');      SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P04',@reg2,N'Hiện trường',N'≤ 2 m/s');      SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P05',@reg2,N'Hiện trường',N'≤ 85 dB(A)');   SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P06',@reg2,N'Thí nghiệm',N'≤ 300 µg/m³');   SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P07',@reg2,N'Thí nghiệm',N'≤ 200 µg/m³');   SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P08',@reg2,N'Thí nghiệm',N'≤ 350 µg/m³');   SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P09',@reg2,N'Thí nghiệm',N'≤ 30000 µg/m³'); SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P10',@reg2,N'Thí nghiệm',N'≤ 200 µg/m³');   SET @piIdx=@piIdx+1;
    END
    ELSE IF @et2 = 2  -- NUOC THAI
    BEGIN
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P11',@reg2,N'Hiện trường',N'5,5–9');        SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P12',@reg2,N'Hiện trường',N'≤ 40 °C');      SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P13',@reg2,N'Thí nghiệm',N'≤ 50 mg/L');     SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P14',@reg2,N'Thí nghiệm',N'≤ 150 mg/L');    SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P15',@reg2,N'Thí nghiệm',N'≤ 100 mg/L');    SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P16',@reg2,N'Thí nghiệm',N'≤ 40 mg/L');     SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P17',@reg2,N'Thí nghiệm',N'≤ 6 mg/L');      SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P18',@reg2,N'Thí nghiệm',N'≤ 10 mg/L');     SET @piIdx=@piIdx+1;
    END
    ELSE  -- DAT
    BEGIN
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P19',@reg2,N'Hiện trường',N'5–8,5');        SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P20',@reg2,N'Thí nghiệm',N'≤ 70 mg/kg');    SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P21',@reg2,N'Thí nghiệm',N'≤ 200 mg/kg');   SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P22',@reg2,N'Thí nghiệm',N'≤ 2 mg/kg');     SET @piIdx=@piIdx+1;
        INSERT INTO SamplingPlanItems (PlanItemID,OrderID,ParamID,RegulationID,Department,QcvnLimit) VALUES ('PI-'+RIGHT('0000'+CAST(@piIdx AS VARCHAR),5),@oId2,'P23',@reg2,N'Thí nghiệm',N'≤ 15 mg/kg');    SET @piIdx=@piIdx+1;
    END

    SET @oi = @oi + 1;
END;
GO

-- ============================================================
-- 11. PHẢN HỒI KHÁCH HÀNG — 10 feedback (đủ 10 KH)
-- ============================================================
PRINT 'Seeding CustomerFeedbacks...';
GO
INSERT INTO CustomerFeedbacks (FeedbackID, CustomerID, ResponseSpeed, ResponseTime, PreviousViolations, Frequency) VALUES
('F01', 'C01',  8, 2, 0,  5),   -- Bao Bì Xanh: phản hồi nhanh, không vi phạm
('F02', 'C02',  5, 5, 2,  2),   -- VinaSteel: phản hồi chậm, 2 vi phạm
('F03', 'C03', 10, 1, 0, 12),   -- Vinalong: xuất sắc, tần suất cao
('F04', 'C04',  3, 7, 1,  3),   -- Dệt may: phản hồi rất chậm
('F05', 'C05',  9, 2, 0,  8),   -- Hóa chất HP: rất tốt
('F06', 'C06',  7, 3, 0,  6),   -- Trung Nguyên: tốt
('F07', 'C07', 10, 1, 0, 10),   -- Vinamilk: xuất sắc
('F08', 'C08',  4, 6, 3,  2),   -- Hòa Phát: khá chậm, 3 vi phạm
('F09', 'C09',  6, 4, 1,  4),   -- Masan: trung bình
('F10', 'C10',  8, 2, 0,  7);   -- Nhựa Bình Minh: tốt
GO

-- ============================================================
-- 12. BỔ SUNG CÁC CỘT CÒN THIẾU (UPDATE)
-- ============================================================
PRINT 'Filling missing columns in Users...';
GO

-- Users: DateOfBirth, Address (IsFaceIDRegistered đã có DEFAULT 0)
UPDATE Users SET
    DateOfBirth = CASE UserID
        WHEN 'U01' THEN '1998-03-15'
        WHEN 'U02' THEN '1975-07-22'
        WHEN 'U03' THEN '2001-11-05'
        WHEN 'U04' THEN '2006-05-12'
        WHEN 'U05' THEN '1990-09-30'
        WHEN 'U06' THEN '1995-02-18'
        WHEN 'U07' THEN '1988-12-01'
        WHEN 'U08' THEN '1993-06-25'
        WHEN 'U09' THEN '1987-04-14'
        WHEN 'U10' THEN '2005-12-29'
        WHEN 'U11' THEN '1997-08-08'
        WHEN 'U12' THEN '1992-01-20'
    END,
    Address = CASE UserID
        WHEN 'U01' THEN N'123 Nguyễn Huệ, Q1, TP.HCM'
        WHEN 'U02' THEN N'45 Lê Lợi, Q1, TP.HCM'
        WHEN 'U03' THEN N'78 Trần Phú, Quận 5, TP.HCM'
        WHEN 'U04' THEN N'12 Hoàng Văn Thụ, Tân Bình, TP.HCM'
        WHEN 'U05' THEN N'56 Cách Mạng Tháng 8, Q3, TP.HCM'
        WHEN 'U06' THEN N'90 Điện Biên Phủ, Bình Thạnh, TP.HCM'
        WHEN 'U07' THEN N'34 Nguyễn Thị Minh Khai, Q3, TP.HCM'
        WHEN 'U08' THEN N'67 Lý Thường Kiệt, Q10, TP.HCM'
        WHEN 'U09' THEN N'21 Hai Bà Trưng, Q1, TP.HCM'
        WHEN 'U10' THEN N'88 Trường Chinh, Tân Bình, TP.HCM'
        WHEN 'U11' THEN N'15 Phan Đăng Lưu, Bình Thạnh, TP.HCM'
        WHEN 'U12' THEN N'33 Võ Thị Sáu, Q3, TP.HCM'
    END,
    UpdatedAt = DATEADD(DAY, -CAST(ABS(CHECKSUM(UserID)) % 30 AS INT), GETDATE()),
    IsFaceIDRegistered = CASE UserID
        WHEN 'U01' THEN 1
        WHEN 'U02' THEN 1
        WHEN 'U04' THEN 1
        WHEN 'U10' THEN 1
        ELSE 0
    END;
GO

-- Contracts: UpdatedAt
PRINT 'Filling missing columns in Contracts...';
GO
UPDATE Contracts
SET UpdatedAt = DATEADD(DAY, CAST(ABS(CHECKSUM(ContractID)) % 10 AS INT), SignedDate)
WHERE UpdatedAt IS NULL;
GO

-- Orders: FinalReportPath, UpdatedAt
PRINT 'Filling missing columns in Orders...';
GO
UPDATE Orders
SET
    FinalReportPath = CASE
        WHEN Status = 2 THEN N'\\Reports\Final\' + OrderID + '_BaoCaoKetQua.pdf'
        ELSE NULL
    END,
    UpdatedAt = CASE
        WHEN Status = 2 THEN DATEADD(DAY, 3, OrderDate)
        ELSE DATEADD(DAY, 1, OrderDate)
    END;
GO

-- Samples: FieldImage, IsWarning, UpdatedAt
PRINT 'Filling missing columns in Samples...';
GO
UPDATE Samples
SET
    FieldImage = N'\\Images\Samples\' + SampleID + '_field.jpg',
    IsWarning  = CASE
        WHEN EXISTS (
            SELECT 1 FROM TestResults tr
            WHERE tr.SampleID = Samples.SampleID AND tr.IsWarning = 1
        ) THEN 1
        ELSE 0
    END,
    UpdatedAt = DATEADD(DAY, 2, SamplingTime);
GO

-- CustomerFeedbacks: CreatedDate (DEFAULT GETDATE() có thể đã được điền, nhưng đặt giá trị cụ thể)
PRINT 'Filling missing columns in CustomerFeedbacks...';
GO
UPDATE CustomerFeedbacks
SET CreatedDate = DATEADD(MONTH, -CAST(ABS(CHECKSUM(FeedbackID)) % 6 AS INT) - 1, GETDATE())
WHERE CreatedDate IS NULL OR CreatedDate >= DATEADD(SECOND, -5, GETDATE());
GO

-- ============================================================
-- 13. RESULT HISTORY — 20 bản ghi chỉnh sửa kết quả (giả lập QC review)
-- ============================================================
PRINT 'Seeding ResultHistory...';
GO
INSERT INTO ResultHistory (HistoryID, ResultID, OldValue, NewValue, ChangedBy, ChangedAt) VALUES
('RH-001', 'RES-00001',  295.0,  320.0, 'U09', DATEADD(DAY, -85, GETDATE())),
('RH-002', 'RES-00002',   28.5,   29.0, 'U09', DATEADD(DAY, -83, GETDATE())),
('RH-003', 'RES-00006',  145.0,  188.0, 'U12', DATEADD(DAY, -80, GETDATE())),
('RH-004', 'RES-00007',   82.0,   95.0, 'U12', DATEADD(DAY, -78, GETDATE())),
('RH-005', 'RES-00011',    6.8,    7.1, 'U09', DATEADD(DAY, -75, GETDATE())),
('RH-006', 'RES-00013',   42.0,   58.5, 'U09', DATEADD(DAY, -72, GETDATE())),
('RH-007', 'RES-00014',  110.0,  138.0, 'U12', DATEADD(DAY, -70, GETDATE())),
('RH-008', 'RES-00021',    5.8,    6.2, 'U09', DATEADD(DAY, -65, GETDATE())),
('RH-009', 'RES-00023',   35.0,   48.0, 'U12', DATEADD(DAY, -62, GETDATE())),
('RH-010', 'RES-00031',  250.0,  285.0, 'U09', DATEADD(DAY, -58, GETDATE())),
('RH-011', 'RES-00032',  180.0,  215.0, 'U09', DATEADD(DAY, -55, GETDATE())),
('RH-012', 'RES-00041',    8.5,    9.2, 'U12', DATEADD(DAY, -50, GETDATE())),
('RH-013', 'RES-00042',   22.5,   26.8, 'U12', DATEADD(DAY, -48, GETDATE())),
('RH-014', 'RES-00051',  105.0,  122.0, 'U09', DATEADD(DAY, -42, GETDATE())),
('RH-015', 'RES-00061',    6.1,    7.4, 'U09', DATEADD(DAY, -38, GETDATE())),
('RH-016', 'RES-00062',  195.0,  210.0, 'U12', DATEADD(DAY, -35, GETDATE())),
('RH-017', 'RES-00071',   88.0,   97.0, 'U09', DATEADD(DAY, -28, GETDATE())),
('RH-018', 'RES-00081',  145.0,  162.0, 'U12', DATEADD(DAY, -22, GETDATE())),
('RH-019', 'RES-00091',   78.0,   85.0, 'U09', DATEADD(DAY, -15, GETDATE())),
('RH-020', 'RES-00101',  312.0,  298.0, 'U12', DATEADD(DAY,  -8, GETDATE()));
GO

-- ============================================================
-- 14. AUDIT LOGS — 40 bản ghi hành động hệ thống
-- ============================================================
PRINT 'Seeding AuditLogs...';
GO
INSERT INTO AuditLogs (LogID, UserID, Action, EntityType, EntityID, Detail, LoggedAt) VALUES
(NEWID(), 'U01', N'LOGIN',           NULL,         NULL,       N'Đăng nhập thành công từ IP 192.168.1.101', DATEADD(DAY, -90, GETDATE())),
(NEWID(), 'U02', N'LOGIN',           NULL,         NULL,       N'Đăng nhập thành công từ IP 192.168.1.102', DATEADD(DAY, -89, GETDATE())),
(NEWID(), 'U03', N'CREATE_CONTRACT', N'Contract',  'HD-0040',  N'Tạo hợp đồng mới cho C10',                DATEADD(DAY, -88, GETDATE())),
(NEWID(), 'U06', N'CREATE_CONTRACT', N'Contract',  'HD-0039',  N'Tạo hợp đồng mới cho C09',                DATEADD(DAY, -87, GETDATE())),
(NEWID(), 'U08', N'CREATE_ORDER',    N'Order',     'ORD-0079', N'Lập lệnh quan trắc cho HD-0040',          DATEADD(DAY, -86, GETDATE())),
(NEWID(), 'U11', N'CREATE_ORDER',    N'Order',     'ORD-0080', N'Lập lệnh quan trắc cho HD-0040',          DATEADD(DAY, -85, GETDATE())),
(NEWID(), 'U04', N'CREATE_SAMPLE',   N'Sample',    'SMP-0001', N'Lấy mẫu tại hiện trường KK',              DATEADD(DAY, -84, GETDATE())),
(NEWID(), 'U10', N'CREATE_SAMPLE',   N'Sample',    'SMP-0002', N'Lấy mẫu tại hiện trường NT',              DATEADD(DAY, -83, GETDATE())),
(NEWID(), 'U05', N'ENTER_RESULT',    N'TestResult',NULL,       N'Nhập kết quả PTN cho SMP-0001 (P06-P10)', DATEADD(DAY, -82, GETDATE())),
(NEWID(), 'U07', N'ENTER_RESULT',    N'TestResult',NULL,       N'Nhập kết quả PTN cho SMP-0002 (P13-P18)', DATEADD(DAY, -81, GETDATE())),
(NEWID(), 'U09', N'APPROVE_RESULT',  N'Order',     'ORD-0039', N'Phê duyệt kết quả quan trắc',             DATEADD(DAY, -80, GETDATE())),
(NEWID(), 'U12', N'APPROVE_RESULT',  N'Order',     'ORD-0040', N'Phê duyệt kết quả quan trắc',             DATEADD(DAY, -79, GETDATE())),
(NEWID(), 'U01', N'UPDATE_USER',     N'User',      'U04',      N'Cập nhật thông tin nhân viên U04',         DATEADD(DAY, -75, GETDATE())),
(NEWID(), 'U01', N'UPDATE_USER',     N'User',      'U10',      N'Cập nhật thông tin nhân viên U10',         DATEADD(DAY, -74, GETDATE())),
(NEWID(), 'U03', N'UPDATE_CONTRACT', N'Contract',  'HD-0035',  N'Gia hạn hợp đồng HD-0035',                DATEADD(DAY, -70, GETDATE())),
(NEWID(), 'U06', N'CREATE_CONTRACT', N'Contract',  'HD-0001',  N'Tạo hợp đồng mới cho C01',                DATEADD(DAY, -9,  GETDATE())),
(NEWID(), 'U03', N'CREATE_CONTRACT', N'Contract',  'HD-0002',  N'Tạo hợp đồng mới cho C02',                DATEADD(DAY, -8,  GETDATE())),
(NEWID(), 'U08', N'CREATE_ORDER',    N'Order',     'ORD-0001', N'Lập lệnh quan trắc cho HD-0001',          DATEADD(DAY, -7,  GETDATE())),
(NEWID(), 'U11', N'CREATE_ORDER',    N'Order',     'ORD-0002', N'Lập lệnh quan trắc cho HD-0001',          DATEADD(DAY, -7,  GETDATE())),
(NEWID(), 'U04', N'CREATE_SAMPLE',   N'Sample',    'SMP-0079', N'Lấy mẫu hiện trường cho ORD-0079',        DATEADD(DAY, -6,  GETDATE())),
(NEWID(), 'U10', N'CREATE_SAMPLE',   N'Sample',    'SMP-0080', N'Lấy mẫu hiện trường cho ORD-0080',        DATEADD(DAY, -6,  GETDATE())),
(NEWID(), 'U05', N'ENTER_RESULT',    N'TestResult',NULL,       N'Nhập kết quả PTN cho SMP-0079',           DATEADD(DAY, -5,  GETDATE())),
(NEWID(), 'U07', N'ENTER_RESULT',    N'TestResult',NULL,       N'Nhập kết quả PTN cho SMP-0080',           DATEADD(DAY, -5,  GETDATE())),
(NEWID(), 'U09', N'APPROVE_RESULT',  N'Order',     'ORD-0003', N'Phê duyệt kết quả lệnh ORD-0003',        DATEADD(DAY, -4,  GETDATE())),
(NEWID(), 'U12', N'APPROVE_RESULT',  N'Order',     'ORD-0004', N'Phê duyệt kết quả lệnh ORD-0004',        DATEADD(DAY, -4,  GETDATE())),
(NEWID(), 'U02', N'VIEW_REPORT',     N'Order',     'ORD-0001', N'Giám đốc xem báo cáo tổng hợp',          DATEADD(DAY, -3,  GETDATE())),
(NEWID(), 'U02', N'VIEW_REPORT',     N'Order',     'ORD-0002', N'Giám đốc xem báo cáo tổng hợp',          DATEADD(DAY, -3,  GETDATE())),
(NEWID(), 'U01', N'LOGIN',           NULL,         NULL,       N'Đăng nhập thành công từ IP 192.168.1.101', DATEADD(DAY, -2,  GETDATE())),
(NEWID(), 'U04', N'LOGIN',           NULL,         NULL,       N'Đăng nhập thành công từ IP 192.168.1.104', DATEADD(DAY, -2,  GETDATE())),
(NEWID(), 'U05', N'LOGIN',           NULL,         NULL,       N'Đăng nhập thành công từ IP 192.168.1.105', DATEADD(DAY, -2,  GETDATE())),
(NEWID(), 'U08', N'LOGIN',           NULL,         NULL,       N'Đăng nhập thành công từ IP 192.168.1.108', DATEADD(DAY, -1,  GETDATE())),
(NEWID(), 'U09', N'LOGIN',           NULL,         NULL,       N'Đăng nhập thành công từ IP 192.168.1.109', DATEADD(DAY, -1,  GETDATE())),
(NEWID(), 'U03', N'SEND_EMAIL',      N'Contract',  'HD-0020',  N'Gửi thông báo sắp hết hạn cho C10',       DATEADD(DAY, -1,  GETDATE())),
(NEWID(), 'U03', N'SEND_EMAIL',      N'Contract',  'HD-0021',  N'Gửi thông báo sắp hết hạn cho C01',       DATEADD(DAY, -1,  GETDATE())),
(NEWID(), 'U01', N'CREATE_USER',     N'User',      'U12',      N'Tạo tài khoản mới cho nhân viên Kết quả', DATEADD(HOUR,-5,  GETDATE())),
(NEWID(), 'U06', N'UPDATE_CONTRACT', N'Contract',  'HD-0022',  N'Cập nhật điều khoản HD-0022',             DATEADD(HOUR,-4,  GETDATE())),
(NEWID(), 'U09', N'APPROVE_RESULT',  N'Order',     'ORD-0005', N'Phê duyệt kết quả lệnh ORD-0005',        DATEADD(HOUR,-3,  GETDATE())),
(NEWID(), 'U12', N'APPROVE_RESULT',  N'Order',     'ORD-0006', N'Phê duyệt kết quả lệnh ORD-0006',        DATEADD(HOUR,-2,  GETDATE())),
(NEWID(), 'U02', N'LOGIN',           NULL,         NULL,       N'Giám đốc đăng nhập từ IP 192.168.1.102',  DATEADD(HOUR,-1,  GETDATE())),
(NEWID(), 'U02', N'VIEW_DASHBOARD',  NULL,         NULL,       N'Xem tổng quan dashboard',                  GETDATE());
GO

-- ============================================================
-- 15. EMAIL QUEUE — 15 email thông báo
-- ============================================================
PRINT 'Seeding EmailQueue...';
GO
INSERT INTO EmailQueue (EmailID, Recipient, Subject, Body, AttachmentPath, Status, CreatedTime, Type) VALUES
-- Gửi thành công (Status=1)
('EQ-001', 'contact@baobixanh.vn',       N'[ECOVA] Kết quả quan trắc HD-0001',          N'Kính gửi Quý khách hàng Công ty CP Bao Bì Xanh, chúng tôi gửi kèm kết quả quan trắc môi trường của lệnh ORD-0001.', N'\\Reports\Final\ORD-0001_BaoCaoKetQua.pdf', 1, DATEADD(DAY,-85,GETDATE()), N'ResultReport'),
('EQ-002', 'info@vinasteel.vn',           N'[ECOVA] Kết quả quan trắc HD-0002',          N'Kính gửi Quý khách hàng Tập đoàn VinaSteel, chúng tôi gửi kèm kết quả quan trắc môi trường của lệnh ORD-0003.', N'\\Reports\Final\ORD-0003_BaoCaoKetQua.pdf', 1, DATEADD(DAY,-80,GETDATE()), N'ResultReport'),
('EQ-003', 'admin@vinalong.vn',           N'[ECOVA] Kết quả quan trắc HD-0003',          N'Kính gửi Quý khách hàng Nhà máy Sữa Vinalong, đây là kết quả quan trắc mới nhất.', N'\\Reports\Final\ORD-0005_BaoCaoKetQua.pdf', 1, DATEADD(DAY,-75,GETDATE()), N'ResultReport'),
('EQ-004', 'info@textile-tb.com',         N'[ECOVA] Kết quả quan trắc HD-0004',          N'Kính gửi Quý khách hàng Dệt may Thái Bình, chúng tôi gửi kết quả quan trắc kỳ này.', NULL, 1, DATEADD(DAY,-70,GETDATE()), N'ResultReport'),
('EQ-005', 'contact@chemicalhp.com',      N'[ECOVA] Kết quả quan trắc HD-0005',          N'Kính gửi Quý khách hàng Hóa chất Hiệp Phước, đính kèm báo cáo quan trắc môi trường.', N'\\Reports\Final\ORD-0009_BaoCaoKetQua.pdf', 1, DATEADD(DAY,-65,GETDATE()), N'ResultReport'),
('EQ-006', 'info@trungnguyen.com.vn',     N'[ECOVA] Báo cáo định kỳ Q3/2025',            N'Kính gửi Trung Nguyên Legend, đính kèm báo cáo tổng hợp quan trắc quý 3 năm 2025.', NULL, 1, DATEADD(DAY,-60,GETDATE()), N'QuarterlyReport'),
('EQ-007', 'contact@vinamilk.com.vn',     N'[ECOVA] Báo cáo định kỳ Q3/2025',            N'Kính gửi Vinamilk, đính kèm báo cáo tổng hợp quan trắc quý 3 năm 2025.', NULL, 1, DATEADD(DAY,-58,GETDATE()), N'QuarterlyReport'),
('EQ-008', 'info@hoaphat.com.vn',         N'[ECOVA] Cảnh báo vượt ngưỡng QCVN',         N'Kính gửi Hòa Phát Group, hệ thống ghi nhận một số chỉ tiêu vượt ngưỡng cho phép trong lần quan trắc gần nhất. Đề nghị quý đơn vị xem xét và có biện pháp khắc phục.', NULL, 1, DATEADD(DAY,-50,GETDATE()), N'Warning'),
('EQ-009', 'info@masangroup.com',         N'[ECOVA] Kết quả quan trắc HD-0009',          N'Kính gửi Tập đoàn Masan, chúng tôi gửi kết quả quan trắc môi trường định kỳ.', NULL, 1, DATEADD(DAY,-45,GETDATE()), N'ResultReport'),
('EQ-010', 'contact@binhminhplastic.com', N'[ECOVA] Báo cáo định kỳ Q4/2025',            N'Kính gửi Nhựa Bình Minh, đính kèm báo cáo tổng hợp quan trắc quý 4 năm 2025.', NULL, 1, DATEADD(DAY,-30,GETDATE()), N'QuarterlyReport'),
-- Đang chờ gửi (Status=0) — thông báo sắp hết hạn hợp đồng
('EQ-011', 'contact@baobixanh.vn',       N'[ECOVA] Thông báo: Hợp đồng HD-0021 sắp hết hạn', N'Kính gửi Công ty CP Bao Bì Xanh, hợp đồng HD-0021 sẽ hết hạn trong 20 ngày. Đề nghị liên hệ ECOVA để gia hạn hợp đồng.', NULL, 0, DATEADD(DAY,-1,GETDATE()), N'ContractExpiry'),
('EQ-012', 'info@vinasteel.vn',           N'[ECOVA] Thông báo: Hợp đồng HD-0022 sắp hết hạn', N'Kính gửi Tập đoàn VinaSteel, hợp đồng HD-0022 sẽ hết hạn trong 15 ngày. Đề nghị liên hệ ECOVA để gia hạn hợp đồng.', NULL, 0, DATEADD(DAY,-1,GETDATE()), N'ContractExpiry'),
('EQ-013', 'admin@vinalong.vn',           N'[ECOVA] Thông báo: Hợp đồng HD-0023 sắp hết hạn', N'Kính gửi Nhà máy Sữa Vinalong, hợp đồng HD-0023 sẽ hết hạn trong 25 ngày. Đề nghị liên hệ ECOVA để gia hạn hợp đồng.', NULL, 0, GETDATE(), N'ContractExpiry'),
-- Gửi thất bại (Status=2)
('EQ-014', 'info@hoaphat.com.vn',         N'[ECOVA] Kết quả quan trắc HD-0010',          N'Kính gửi Hòa Phát Group, đây là kết quả quan trắc hàng quý.', N'\\Reports\Final\ORD-0019_BaoCaoKetQua.pdf', 2, DATEADD(DAY,-20,GETDATE()), N'ResultReport'),
('EQ-015', 'invalid-email@xxx',           N'[ECOVA] Test notification',                   N'Nội dung test.', NULL, 2, DATEADD(DAY,-10,GETDATE()), N'System');
GO

-- ============================================================
-- 16. TỔNG KẾT
-- ============================================================
PRINT '';
PRINT '=== ALL SEED DATA APPLIED SUCCESSFULLY ===';
PRINT '';
PRINT 'THONG KE:';
PRINT '  - 7 Roles';
PRINT '  - 12 Users (co DateOfBirth, Address, IsFaceIDRegistered, UpdatedAt)';
PRINT '  - 10 Customers';
PRINT '  - 3 Regulations (Khong khi, Nuoc thai, Dat)';
PRINT '  - 24 TestParameters (Field + Lab)';
PRINT '  - 24 RegulationLimits (ca Field + Lab)';
PRINT '  - 40 Contracts (HD-0001 .. HD-0040, co UpdatedAt)';
PRINT '  - 80 Orders (2 khu vuc/hop dong, co FinalReportPath + UpdatedAt)';
PRINT '  - 80 Samples (co FieldImage, IsWarning, UpdatedAt)';
PRINT '  - ~800 TestResults (du 40 HD, 10 KK + 8 NT + 6 Dat)';
PRINT '  - SamplingPlanItems: 28 orders x 5-10 params';
PRINT '  - 10 CustomerFeedbacks (co CreatedDate)';
PRINT '  - 20 ResultHistory (lich su chinh sua ket qua)';
PRINT '  - 40 AuditLogs (hanh dong he thong: login, CRUD, approve)';
PRINT '  - 15 EmailQueue (10 Sent, 3 Pending, 2 Failed)';
PRINT '';
PRINT 'TAI KHOAN (mat khau: admin):';
PRINT '  admin        / admin  -> R01 Quan tri  | Nguyen Trung Nguyen';
PRINT '  director     / admin  -> R02 Giam doc   | Pham Ngoc Anh';
PRINT '  tuyethan     / admin  -> R03 Kinh doanh | Ho Thi Tuyet Han';
PRINT '  minhkhoa     / admin  -> R03 Kinh doanh | Tran Minh Khoa';
PRINT '  baolongnv    / admin  -> R04 Hien truong| Luong Bao Long';
PRINT '  duchuy       / admin  -> R04 Hien truong| Le Duc Huy';
PRINT '  thuha        / admin  -> R05 Thi nghiem | Nguyen Thi Thu Ha';
PRINT '  anhtuan      / admin  -> R05 Thi nghiem | Tran Anh Tuan';
PRINT '  huonggiang   / admin  -> R06 Ke hoach   | Do Thi Huong Giang';
PRINT '  minhtung     / admin  -> R06 Ke hoach   | Vo Minh Tung';
PRINT '  thanhloannv  / admin  -> R07 Ket qua    | Bui Thi Thanh Loan';
PRINT '  hoangnam     / admin  -> R07 Ket qua    | Nguyen Hoang Nam';
GO

