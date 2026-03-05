-- Tạo Database
CREATE DATABASE DoAnTotNghiep_Test;
GO
USE DoAnTotNghiep_Test;
GO

-- =============================================
-- NHÓM 1: NGƯỜI DÙNG VÀ CẤU HÌNH HỆ THỐNG
-- =============================================
-- 1. Bảng Người dùng
CREATE TABLE NguoiDung (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    TenDangNhap NVARCHAR(50) NOT NULL UNIQUE,
    MatKhau NVARCHAR(255) NOT NULL, -- Mật khẩu đã Hash
    HoTen NVARCHAR(250),
    Email NVARCHAR(100) UNIQUE,
    SDT NVARCHAR(10),
    Avt NVARCHAR(255),
    VaiTro NVARCHAR(20) CHECK (VaiTro IN ('Admin','Staff','Customer')),
    NgayTao DATETIME DEFAULT GETDATE(),
    NgayCapNhat DATETIME,
    TrangThai BIT DEFAULT 1 -- 1: Hoạt động, 0: Khóa
);
GO

-- 2. Bảng Địa chỉ
CREATE TABLE DiaChi (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    NguoiDungID INT NOT NULL,
    TenNguoiNhan NVARCHAR(200),
    SDT_NguoiNhan NVARCHAR(10),
    DiaChiChiTiet NVARCHAR(255),
    PhuongXa NVARCHAR(100),
    Tinh_ThanhPho NVARCHAR(100),
    MacDinh BIT DEFAULT 0,
    FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(ID)
);
GO

-- 3. Bảng Phương thức thanh toán
CREATE TABLE PhuongThucThanhToan (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    TenPhuongThuc NVARCHAR(255),
    MaCode NVARCHAR(50), -- COD, VNPAY, BANKING
    MoTa NVARCHAR(MAX),
    HinhAnh NVARCHAR(255),
    TrangThai BIT DEFAULT 1
);
GO



-- =============================================
-- NHÓM 2: SẢN PHẨM
-- =============================================
-- 4. Bảng Danh mục
CREATE TABLE DanhMuc (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    TenDanhMuc NVARCHAR(255) NOT NULL,
    Slug NVARCHAR(255),
    DanhMucChaID INT NULL, -- Self-referencing cho danh mục con
    MoTa NVARCHAR(255),
    HinhAnh NVARCHAR(255),
    NgayTao DATETIME DEFAULT GETDATE(),
    NgayCapNhat DATETIME,
    FOREIGN KEY (DanhMucChaID) REFERENCES DanhMuc(ID)
);
GO

-- 5. Bảng Sản phẩm (Thông tin chung)
CREATE TABLE SanPham (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    MaSanPham NVARCHAR(50) UNIQUE, -- VD: AT-001
    TenSanPham NVARCHAR(200) NOT NULL,
    Slug NVARCHAR(200),
    MoTa NVARCHAR(MAX),
    DanhMucID INT,
    GiaGoc DECIMAL(18, 0),
    GiaBan DECIMAL(18, 0),
    ChatLieu NVARCHAR(255),
    KieuDang NVARCHAR(255),
    LuotXem INT DEFAULT 0,
    TrangThai INT DEFAULT 1, -- 1: Đang bán, 0: Ngừng bán, 2: Tạm hết
    NgayTao DATETIME DEFAULT GETDATE(),
    NgayCapNhat DATETIME,
    FOREIGN KEY (DanhMucID) REFERENCES DanhMuc(ID)
);
GO

-- 6. Bảng Màu sắc
CREATE TABLE MauSac (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    TenMau NVARCHAR(155), -- Đen, Trắng...
    MaHex NVARCHAR(20) -- #000000
);
GO

-- 7. Bảng Kích thước
CREATE TABLE KichThuoc (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    TenSize NVARCHAR(50) -- S, M, L, 29, 30...
);
GO

-- 8. Bảng Biến thể sản phẩm (SKU) - CẬP NHẬT THEO ẢNH BẠN GỬI
CREATE TABLE BienTheSanPham (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    SanPhamID INT NOT NULL,
    MauSacID INT NOT NULL,
    KichThuocID INT NOT NULL,
    SoLuong INT DEFAULT 0, -- Số lượng thực tế trong kho
    SoLuongTamGiu INT DEFAULT 0, -- Số lượng khách đặt nhưng chưa giao
    SKU NVARCHAR(100) UNIQUE, -- Mã định danh duy nhất (VD: AT001-DEN-M)
    FOREIGN KEY (SanPhamID) REFERENCES SanPham(ID),
    FOREIGN KEY (MauSacID) REFERENCES MauSac(ID),
    FOREIGN KEY (KichThuocID) REFERENCES KichThuoc(ID)
);
GO

-- 9. Bảng Ảnh sản phẩm
CREATE TABLE AnhSanPham (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    SanPhamID INT NOT NULL,
    MauSacID INT NULL, -- Null nếu là ảnh chung, có giá trị nếu ảnh theo màu
    URL NVARCHAR(255),
    MacDinh BIT DEFAULT 0,
    FOREIGN KEY (SanPhamID) REFERENCES SanPham(ID),
    FOREIGN KEY (MauSacID) REFERENCES MauSac(ID)
);
GO


-- =============================================
-- NHÓM 3: KHO & NHẬP HÀNG
-- =============================================

-- 10. Bảng Lịch sử kho
CREATE TABLE LichSuKho (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    BienTheSanPhamID INT NOT NULL,
    SoLuongBienDong INT, -- Số dương (+) hoặc âm (-)
    TonThucTeSauBienDong INT,
    LoaiGiaoDich NVARCHAR(255), -- Nhập hàng, Bán hàng, Hoàn trả, Hủy đơn
    MaThamChieu NVARCHAR(255), -- Mã đơn hàng hoặc Mã phiếu nhập
    NgayGhi DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (BienTheSanPhamID) REFERENCES BienTheSanPham(ID)
);
GO




-- =============================================
-- NHÓM 4: BÁN HÀNG
-- =============================================

-- 11. Bảng Giỏ hàng
CREATE TABLE GioHang (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    NguoiDungID INT,
    NgayTao DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(ID)
);
GO

-- 12. Bảng Giỏ hàng chi tiết
CREATE TABLE GioHangChiTiet (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    GioHangID INT NOT NULL,
    BienTheSanPhamID INT NOT NULL,
    SoLuong INT DEFAULT 1,
    FOREIGN KEY (GioHangID) REFERENCES GioHang(ID),
    FOREIGN KEY (BienTheSanPhamID) REFERENCES BienTheSanPham(ID)
);
GO

-- 13. Bảng Đơn hàng
CREATE TABLE DonHang (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    MaDonHang NVARCHAR(155) UNIQUE, -- VD: ORD-2026-011
    NguoiDungID INT  NULL,
    NgayDat DATETIME DEFAULT GETDATE(),
    TenNguoiNhan NVARCHAR(100), -- Snapshot từ DiaChi
    SDT_NguoiNhan NVARCHAR(10),
    EmailNguoiNhan NVARCHAR(100),
    DiaChiGiaoHang NVARCHAR(255),
    TongTien DECIMAL(18, 2), -- Tổng tiền hàng
    PhiVanChuyen DECIMAL(18, 2),
    GiamGia DECIMAL(18, 2),
    TongThanhToan DECIMAL(18, 2), -- Khách phải trả
    PhuongThucThanhToanID INT,
    TrangThaiThanhToan BIT DEFAULT 0, -- 0: Chưa TT, 1: Đã TT
    TrangThaiDonHang INT DEFAULT 0, -- 0: Chờ XN, 1: Đã XN, 2: Đang giao, 3: Hoàn thành, 4: Hủy
    LyDoHuy NVARCHAR(255),
    FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(ID),
    FOREIGN KEY (PhuongThucThanhToanID) REFERENCES PhuongThucThanhToan(ID)
);
GO

-- 14. Bảng Chi tiết đơn hàng
CREATE TABLE ChiTietDonHang (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    DonHangID INT NOT NULL,
    BienTheSanPhamID INT NOT NULL,
    SoLuong INT,
    DonGia DECIMAL(18, 2), -- Giá tại thời điểm mua
    ThanhTien DECIMAL(18, 2),
    FOREIGN KEY (DonHangID) REFERENCES DonHang(ID),
    FOREIGN KEY (BienTheSanPhamID) REFERENCES BienTheSanPham(ID)
);
GO




-- =============================================
-- NHÓM 5: MARKETING & TƯƠNG TÁC
-- =============================================

-- 15. Bảng Mã giảm giá
CREATE TABLE MaGiamGia (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    MaCode NVARCHAR(50) UNIQUE, -- VD: SUMMER2026
    LoaiGiam NVARCHAR(155), -- 'PERCENT' hoặc 'AMOUNT'
    GiaTri DECIMAL(18, 2), -- 10 (%) hoặc 50000 (VND)
    DonToiThieu DECIMAL(18, 2),
    SoLuong INT,
    NgayHetHan DATETIME
);
GO

-- 16. Bảng Đánh giá
CREATE TABLE DanhGia (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    SanPhamID INT NOT NULL,
    NguoiDungID INT NOT NULL,
    SoSao INT CHECK (SoSao >= 1 AND SoSao <= 5),
    BinhLuan NVARCHAR(MAX),
    NgayDanhGia DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (SanPhamID) REFERENCES SanPham(ID),
    FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(ID)
);
GO

-- 17. Bảng Sản phẩm yêu thích
CREATE TABLE SanPhamYeuThich (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    NguoiDungID INT NOT NULL,
    SanPhamID INT NOT NULL,
    NgayThem DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(ID),
    FOREIGN KEY (SanPhamID) REFERENCES SanPham(ID)
);
GO



-- =============================================
-- NHÓM 6: AI TRACKING VÀ GỢI Ý
-- =============================================

-- 18. Bảng Lịch sử xem
CREATE TABLE LichSuXem (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    NguoiDungID INT NOT NULL,
    SanPhamID INT NOT NULL,
    ThoiGian DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(ID),
    FOREIGN KEY (SanPhamID) REFERENCES SanPham(ID)
);
GO

-- 19. Bảng Lịch sử tìm kiếm
CREATE TABLE LichSuTimKiem (
    ID BIGINT IDENTITY(1,1) PRIMARY KEY,
    NguoiDungID INT,
    TuKhoa NVARCHAR(255),
    ThoiGian DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(ID)
);
GO

-- 20. Bảng Gợi ý sản phẩm (AI Results)
CREATE TABLE GoiYSanPham (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    NguoiDungID INT NULL, -- Null = Gợi ý chung, Có ID = Gợi ý cá nhân hóa
    SanPhamNguonID INT, -- Sản phẩm đang xem (Context)
    SanPhamDuocGoiYID INT NOT NULL, -- Sản phẩm AI khuyên mua
    DiemGoiY FLOAT, -- Độ tương đồng/Điểm số
    LoaiGoiY NVARCHAR(250), -- SIMILAR, BUNDLE, HISTORY, TRENDING
    ThoiDiemTinhToan DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(ID),
    FOREIGN KEY (SanPhamNguonID) REFERENCES SanPham(ID),
    FOREIGN KEY (SanPhamDuocGoiYID) REFERENCES SanPham(ID)
);
GO

-- =============================================
-- NHÓM 7: PHỤ TRỢ
-- =============================================

-- 21. Bảng Tin tức
CREATE TABLE TinTuc (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    TieuDe NVARCHAR(255) NOT NULL,
    Slug NVARCHAR(255),
    TomTat NVARCHAR(500),
    NoiDung NVARCHAR(MAX),
    HinhAnh NVARCHAR(255),
    NgayDang DATETIME DEFAULT GETDATE(),
    NguoiDungID INT, -- Người đăng
    TrangThai BIT DEFAULT 1,
    FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(ID)
);
GO

-- 22. Bảng Cấu hình chung
CREATE TABLE CauHinhChung (
    KeyName NVARCHAR(255) PRIMARY KEY,
    Value NVARCHAR(MAX),
    MoTa NVARCHAR(255)
);
GO