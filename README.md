# 🛍️ Xây dựng Hệ Thống Bán Hàng Online Thông Minh Cho Cửa Hàng Thời Trang

<div align="center">

> **Đồ án Tốt nghiệp** — Hệ thống thương mại điện tử tích hợp trí tuệ nhân tạo hỗ trợ tư vấn thời trang và cá nhân hóa trải nghiệm mua sắm.
> **Sinh viên thực hiện: Nguyễn Huy Công**

</div>

---

## 📋 Mục Lục

- [Giới thiệu](#-giới-thiệu)
- [Tính năng nổi bật](#-tính-năng-nổi-bật)
- [Công nghệ sử dụng](#-công-nghệ-sử-dụng)
- [Kiến trúc hệ thống](#-kiến-trúc-hệ-thống)
- [Cấu trúc thư mục](#-cấu-trúc-thư-mục)
- [Yêu cầu hệ thống](#-yêu-cầu-hệ-thống)
- [Hướng dẫn cài đặt](#-hướng-dẫn-cài-đặt)
- [Hướng dẫn sử dụng](#-hướng-dẫn-sử-dụng)
- [Thành viên nhóm](#-thành-viên-nhóm)

---

## 🎯 Giới thiệu

**Hệ Thống Bán Hàng Online Thông Minh Cho Cửa Hàng Thời Trang** là một nền tảng thương mại điện tử hiện đại, được phát triển như đồ án tốt nghiệp đại học. Hệ thống không chỉ cung cấp đầy đủ các chức năng của một sàn thương mại điện tử truyền thống mà còn tích hợp module **AI như Chatbot, hệ thống gợi ý sản phẩm, tìm kiếm sản phẩm bằng hình ảnh** nhằm hỗ trợ người dùng trong việc tìm kiếm và lựa chọn sản phẩm phù hợp.

Dự án hướng tới mục tiêu xây dựng một hệ thống bán hàng hoàn chỉnh, có khả năng mở rộng và ứng dụng thực tế trong lĩnh vực thời trang trực tuyến tại Việt Nam.

---

## ✨ Tính năng nổi bật

### 👤 Phía Người Dùng (Customer)
- Đăng ký, đăng nhập, quản lý tài khoản cá nhân
- Duyệt sản phẩm, tìm kiếm và lọc theo danh mục, giá
- **Tìm kiếm bằng hình ảnh** — nhận diện trang phục qua AI và gợi ý sản phẩm tương tự
- Quản lý giỏ hàng, đặt hàng thanh toán (COD & VNPay) và theo dõi trạng thái đơn hàng
- Đánh giá  sản phẩm
- **Xem sản phẩm gợi ý**
- **Trò chuyện với Chatbot**
- Xem tin tức, bài viết
- Liên hệ

### 🛠️ Phía Quản Trị (Admin)
- Dashboard tổng quan
- Quản lý danh mục, sản phẩm, tồn kho, mã khuyến mại, phương thức thanh toán
- Xử lý và cập nhật trạng thái đơn hàng
- Quản lý tài khoản người dùng và phân quyền
- Báo cáo thống kê theo thời gian thực
- Quản lý tin tức bài viết, cấu hình chung hệ thống

### 🤖 Module AI (AI Fashion Service)
- Nhận diện loại trang phục từ hình ảnh người dùng tải lên
- Gợi ý sản phẩm tương tự dựa trên đặc trưng hình ảnh
- Phân tích xu hướng thời trang hỗ trợ cá nhân hóa trải nghiệm
- Chatbot AI
- Hệ thống gợi ý (Gợi ý cá nhân hóa, gợi ý sản phẩm tương tự/thường mua cùng, gợi ý sản phẩm cho người mới)

---

## 🛠️ Công nghệ sử dụng

| Thành phần | Công nghệ |
|---|---|
| **Backend** | ASP.NET Core (C#) |
| **Frontend** | JavaScript, HTML5, CSS3 |
| **AI Service** | Python (Flask / FastAPI), GeminiAPI, CohereAPI, PineConeAPI... |
| **Cơ sở dữ liệu** | Microsoft SQL Server (T-SQL) |
| **ORM** | Entity Framework Core |
| **Authentication** | ASP.NET Identity / JWT |
| **Version Control** | Git & GitHub |

---

## 🏗️ Kiến trúc hệ thống

```
┌─────────────────────────────────────────────────────┐
│                   CLIENT (Browser)                   │
│              HTML / CSS / JavaScript                 │
└──────────────────────┬──────────────────────────────┘
                       │ HTTP/HTTPS
┌──────────────────────▼──────────────────────────────┐
│              WEB APPLICATION (DoAn_Ver2)             │
│               ASP.NET Core MVC / API                 │
│                                                      │
│  ┌─────────────┐  ┌──────────────┐  ┌─────────────┐ │
│  │  Controllers│  │   Services   │  │ Repositories│ │
│  └─────────────┘  └──────────────┘  └─────────────┘ │
└───────────┬──────────────────────────────┬──────────┘
            │                              │
┌───────────▼──────────┐      ┌────────────▼──────────┐
│   SQL Server Database │      │  AI Fashion Service   │
│        (T-SQL)        │      │  (Python REST API)    │
└──────────────────────┘      └───────────────────────┘
```

---

## 📁 Cấu trúc thư mục

```
HeThongBanHangOnlineThongMinh_DATN/
│
├── DoAn_Ver2/              # Ứng dụng web chính (ASP.NET Core)
│   ├── Controllers/        # Xử lý request từ client
│   ├── Models/             # Các entity và view model
│   ├── Views/              # Giao diện người dùng (Razor / HTML)
│   ├── Services/           # Business logic
│   ├── wwwroot/            # Static files (JS, CSS, Images)
│   └── appsettings.json    # Cấu hình ứng dụng
│
├── AI_Fashion_Service/     # Dịch vụ AI gợi ý sản phẩm và tìm kiếm sàn phẩm bằng hình ảnh (Python)
│   ├── model/              # Model AI đã huấn luyện
│   ├── app.py              # Entry point của AI service
│   └── requirements.txt    # Các thư viện Python cần thiết
│
├── Data/                   # Dữ liệu mẫu và script cơ sở dữ liệu
│   └── *.sql               # Script tạo bảng và dữ liệu mẫu
│
└── README.md
```

---

## ⚙️ Yêu cầu hệ thống

### Môi trường chạy chính (DoAn_Ver2)
- [.NET 6.0 SDK](https://dotnet.microsoft.com/download) trở lên
- [Microsoft SQL Server 2019](https://www.microsoft.com/en-us/sql-server) trở lên
- Visual Studio 2022 hoặc VS Code

### Môi trường AI Service
- Python 3.8 trở lên
- pip (Python package manager)

---

## 🚀 Hướng dẫn cài đặt

### 1. Clone repository

```bash
git clone https://github.com/HuyCong211/HeThongBanHangOnlineThongMinh_DATN.git
cd HeThongBanHangOnlineThongMinh_DATN
```

### 2. Thiết lập cơ sở dữ liệu

Mở **SQL Server Management Studio (SSMS)** và thực thi các file script trong thư mục `Data/`:

```sql
-- Chạy file tạo database và bảng
-- Chạy file insert dữ liệu mẫu
```

### 3. Cấu hình ứng dụng web (DoAn_Ver2)

Mở file `DoAn_Ver2/appsettings.json` và cập nhật connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=BanHangOnline;Trusted_Connection=True;"
  }
}
```

Sau đó chạy ứng dụng:

```bash
cd DoAn_Ver2
dotnet restore
dotnet run
```

### 4. Khởi động AI Fashion Service

```bash
cd AI_Fashion_Service
pip install -r requirements.txt
python app.py
```

### 5. Truy cập ứng dụng

| Service | URL |
|---|---|
| Web Application | `https://localhost:5001` |
| AI Fashion API | `http://localhost:5000` |

---

## 📖 Hướng dẫn sử dụng

### Tài khoản mặc định (Demo)

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Admin | `admin@admin.com` | `Admin@123` |
| Người dùng | `user@test.com` | `User@123` |

> ⚠️ Vui lòng thay đổi mật khẩu sau khi đăng nhập lần đầu.

### Luồng sử dụng cơ bản

1. **Đăng ký / Đăng nhập** tài khoản người dùng
2. **Duyệt sản phẩm** theo danh mục hoặc tìm kiếm theo từ khóa
3. **Tìm kiếm bằng ảnh**: tải lên hình ảnh trang phục để hệ thống AI gợi ý sản phẩm phù hợp
4. **Thêm vào giỏ hàng** và tiến hành đặt hàng
5. **Theo dõi đơn hàng** trong phần quản lý tài khoản

---

## 👨‍💻 Thành viên nhóm

| Họ và tên | Vai trò | GitHub |
|---|---|---|
| Nguyễn Huy Công | Trưởng nhóm — Fullstack & AI | [@HuyCong211](https://github.com/HuyCong211) |

---

## 📄 Giấy phép

Dự án này được phát triển phục vụ mục đích học thuật (Đồ án Tốt nghiệp). Vui lòng liên hệ tác giả trước khi sử dụng cho mục đích thương mại.

---

<div align="center">

⭐ Nếu dự án hữu ích, hãy để lại một **Star** để ủng hộ nhóm!

**© 2025 — Đồ án Tốt nghiệp | Hệ Thống Bán Hàng Online Thông Minh**

</div>
