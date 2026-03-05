SELECT 
    p.ID, 
    p.TenSanPham, 
    p.GiaBan, 
    d.TenDanhMuc,
    -- Tạo link ảnh giả lập hoặc lấy link thật nếu có
    'https://localhost:44338/Product/Detail/' + CAST(p.ID AS VARCHAR) as LinkSanPham
FROM SanPham p
JOIN DanhMuc d ON p.DanhMucID = d.ID
WHERE p.TrangThai = 1