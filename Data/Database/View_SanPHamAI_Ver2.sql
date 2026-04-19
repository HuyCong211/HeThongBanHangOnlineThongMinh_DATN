ALTER VIEW v_AI_SanPham AS
SELECT 
    sp.ID AS SanPhamID,
    sp.TenSanPham,
    dm.TenDanhMuc,
    sp.MoTa,
    sp.ChatLieu,
    sp.KieuDang,
    sp.GiaBan,
    sp.Tags, -- [M?I] G?i tr?c ti?p c?t ch?a Tag t? b?ng S?n ph?m
    
    -- G?p t?t c? Màu S?c c?a s?n ph?m thành 1 chu?i phân cách b?ng d?u ph?y
    (SELECT STRING_AGG(TenMau, ', ') 
     FROM (SELECT DISTINCT ms.TenMau 
           FROM BienTheSanPham bt 
           JOIN MauSac ms ON bt.MauSacID = ms.ID 
           WHERE bt.SanPhamID = sp.ID) as tempMau) AS MauSacHienCo,
           
    -- G?p t?t c? Kích Th??c c?a s?n ph?m thành 1 chu?i
    (SELECT STRING_AGG(TenSize, ', ') 
     FROM (SELECT DISTINCT kt.TenSize 
           FROM BienTheSanPham bt 
           JOIN KichThuoc kt ON bt.KichThuocID = kt.ID 
           WHERE bt.SanPhamID = sp.ID) as tempSize) AS KichThuocHienCo
FROM 
    SanPham sp
LEFT JOIN 
    DanhMuc dm ON sp.DanhMucID = dm.ID
WHERE 
    sp.TrangThai = 1; -- Ch? l?y s?n ph?m ?ang b?t