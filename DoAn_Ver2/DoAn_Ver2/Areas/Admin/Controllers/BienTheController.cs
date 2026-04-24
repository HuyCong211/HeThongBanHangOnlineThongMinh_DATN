using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DoAn_Ver2.Models;
using DoAn_Ver2.Common;
using System.IO;

namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class BienTheController : BaseController
    {
        // GET: Admin/BienThe
        public ActionResult Index(int sanPhamId)
        {
            var sanPham = _unitOfWork.Repository<SanPham>().GetById(sanPhamId);
            if (sanPham == null) return HttpNotFound();

            ViewBag.SanPham = sanPham;

            // Lấy danh sách biến thể của sản phẩm này
            var list = _unitOfWork.Repository<BienTheSanPham>()
                        .GetMany(x => x.SanPhamID == sanPhamId)
                        .OrderByDescending(x => x.ID)
                        .ToList();

            // Lấy dữ liệu nền để hiển thị tên Màu/Size
            ViewBag.ListMau = _unitOfWork.Repository<MauSac>().GetAll().ToList();
            ViewBag.ListSize = _unitOfWork.Repository<KichThuoc>().GetAll().ToList();

            // Lấy danh sách ảnh đã gán cho màu sắc (để hiển thị ra bảng)
            ViewBag.ListAnhMau = _unitOfWork.Repository<AnhSanPham>()
                                    .GetMany(x => x.SanPhamID == sanPhamId && x.MauSacID != null)
                                    .ToList();

            return View(list);
        }
        // 2. THÊM BIẾN THỂ MỚI (GET)
        public ActionResult Create(int sanPhamId)
        {
            var sanPham = _unitOfWork.Repository<SanPham>().GetById(sanPhamId);
            if (sanPham == null) return HttpNotFound();

            ViewBag.SanPham = sanPham;
            ViewBag.MauSacID = new SelectList(_unitOfWork.Repository<MauSac>().GetAll(), "ID", "TenMau");
            ViewBag.KichThuocID = new SelectList(_unitOfWork.Repository<KichThuoc>().GetAll(), "ID", "TenSize");

            //  Lấy danh sách ảnh của sản phẩm để chọn ---
            ViewBag.ListAnhSanPham = _unitOfWork.Repository<AnhSanPham>()
                                                .GetMany(x => x.SanPhamID == sanPhamId)
                                                .ToList();
            return View();
        }

        // 3. THÊM BIẾN THỂ MỚI (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(BienTheSanPham model, HttpPostedFileBase ImageFile, int? SelectedImageId)
        {
            var sanPham = _unitOfWork.Repository<SanPham>().GetById(model.SanPhamID);

            if (ModelState.IsValid)
            {
                // 1. CHECK TRÙNG LẶP (Cặp Màu + Size đã tồn tại chưa?)
                var isExist = _unitOfWork.Repository<BienTheSanPham>()
                                .GetMany(x => x.SanPhamID == model.SanPhamID
                                           && x.MauSacID == model.MauSacID
                                           && x.KichThuocID == model.KichThuocID).Any();

                if (isExist)
                {
                    ModelState.AddModelError("", "Biến thể (Màu + Size) này đã tồn tại!");
                }
                else
                {
                    // 2. TỰ ĐỘNG TẠO MÃ SKU (Nếu trống)
                    if (string.IsNullOrEmpty(model.SKU))
                    {
                        var mau = _unitOfWork.Repository<MauSac>().GetById(model.MauSacID);
                        var size = _unitOfWork.Repository<KichThuoc>().GetById(model.KichThuocID);

                        // Format: MA_SP - SLUG_MAU - SIZE
                        model.SKU = $"{sanPham.MaSanPham}-{MyTools.GenerateSlug(mau.TenMau).ToUpper()}-{size.TenSize}";
                    }

                    // Check trùng mã SKU
                    var isSkuExist = _unitOfWork.Repository<BienTheSanPham>().GetMany(x => x.SKU == model.SKU).Any();
                    if (isSkuExist)
                    {
                        ModelState.AddModelError("SKU", "Mã SKU này đã tồn tại.");
                    }
                    else
                    {
                        // 3. LƯU BIẾN THỂ
                        model.SoLuongTamGiu = 0;
                        _unitOfWork.Repository<BienTheSanPham>().Add(model);

                        // 4. XỬ LÝ ẢNH ĐẠI DIỆN CHO MÀU 
                        if (ImageFile != null && ImageFile.ContentLength > 0)
                        {
                            string fileName = Path.GetFileName(ImageFile.FileName);
                            string newFileName = sanPham.Slug + "-color-" + model.MauSacID + "-" + DateTime.Now.Ticks + Path.GetExtension(fileName);
                            string path = Path.Combine(Server.MapPath("~/Content/images/sanpham/"), newFileName);

                            // Kiểm tra thư mục
                            if (!Directory.Exists(Server.MapPath("~/Content/images/sanpham/")))
                                Directory.CreateDirectory(Server.MapPath("~/Content/images/sanpham/"));

                            ImageFile.SaveAs(path);

                            // Kiểm tra xem màu này đã có ảnh chưa?
                            var existingImg = _unitOfWork.Repository<AnhSanPham>()
                                                .GetMany(x => x.SanPhamID == model.SanPhamID && x.MauSacID == model.MauSacID)
                                                .FirstOrDefault();

                            if (existingImg == null)
                            {
                                // Chưa có thì thêm mới
                                var anhMau = new AnhSanPham()
                                {
                                    SanPhamID = model.SanPhamID,
                                    MauSacID = model.MauSacID, 
                                    URL = "/Content/images/sanpham/" + newFileName,
                                    MacDinh = false
                                };
                                _unitOfWork.Repository<AnhSanPham>().Add(anhMau);
                            }
                            else
                            {
                                // Có rồi thì update URL ảnh cũ (để đồng bộ các size khác cùng màu)
                                existingImg.URL = "/Content/images/sanpham/" + newFileName;
                                _unitOfWork.Repository<AnhSanPham>().Update(existingImg);
                            }
                        }
                        // TH2: Người dùng CHỌN ẢNH CÓ SẴN
                        else if (SelectedImageId.HasValue)
                        {
                            var existingPhoto = _unitOfWork.Repository<AnhSanPham>().GetById(SelectedImageId.Value);
                            if (existingPhoto != null)
                            {
                                existingPhoto.MauSacID = model.MauSacID;
                                _unitOfWork.Repository<AnhSanPham>().Update(existingPhoto);
                            }
                        }

                        _unitOfWork.Save();
                        TempData["Message"] = "Thêm biến thể thành công!";
                        return RedirectToAction("Index", new { sanPhamId = model.SanPhamID });
                    }
                }
            }

            ViewBag.SanPham = sanPham;
            ViewBag.MauSacID = new SelectList(_unitOfWork.Repository<MauSac>().GetAll(), "ID", "TenMau", model.MauSacID);
            ViewBag.KichThuocID = new SelectList(_unitOfWork.Repository<KichThuoc>().GetAll(), "ID", "TenSize", model.KichThuocID);
            ViewBag.ListAnhSanPham = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.SanPhamID == model.SanPhamID).ToList();
            return View(model);
        }

        // ==========================================
        // 3. CẬP NHẬT BIẾN THỂ 
        // ==========================================

        // GET: Hiển thị form sửa
        public ActionResult Edit(int id)
        {
            var model = _unitOfWork.Repository<BienTheSanPham>().GetById(id);
            if (model == null) return HttpNotFound();

            var sanPham = _unitOfWork.Repository<SanPham>().GetById(model.SanPhamID);
            ViewBag.SanPham = sanPham;

            ViewBag.MauSacID = new SelectList(_unitOfWork.Repository<MauSac>().GetAll(), "ID", "TenMau", model.MauSacID);
            ViewBag.KichThuocID = new SelectList(_unitOfWork.Repository<KichThuoc>().GetAll(), "ID", "TenSize", model.KichThuocID);

            // Lấy danh sách tất cả ảnh của sản phẩm
            var listAnh = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.SanPhamID == model.SanPhamID).ToList();
            ViewBag.ListAnhSanPham = listAnh;

            // --- Tìm ID ảnh đang được chọn cho MÀU hiện tại ---
            var currentImg = listAnh.FirstOrDefault(x => x.MauSacID == model.MauSacID);
            ViewBag.SelectedImageId = currentImg != null ? currentImg.ID : (int?)null;
            // -------------------------------------------------------------
            return View(model);
        }

        // POST: Xử lý cập nhật
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(BienTheSanPham model, HttpPostedFileBase ImageFile, int? SelectedImageId)
        {
            var sanPham = _unitOfWork.Repository<SanPham>().GetById(model.SanPhamID);

            if (ModelState.IsValid)
            {
                // 1. CHECK TRÙNG LẶP (Trừ chính nó ra)
                var isDuplicate = _unitOfWork.Repository<BienTheSanPham>()
                                .GetMany(x => x.SanPhamID == model.SanPhamID
                                           && x.MauSacID == model.MauSacID
                                           && x.KichThuocID == model.KichThuocID
                                           && x.ID != model.ID)
                                .Any();

                if (isDuplicate)
                {
                    ModelState.AddModelError("", "Biến thể (Màu + Size) này đã tồn tại ở SKU khác!");
                }
                else
                {
                    // 2. CHECK TRÙNG SKU (Trừ chính nó ra)
                    var isSkuDuplicate = _unitOfWork.Repository<BienTheSanPham>()
                                            .GetMany(x => x.SKU == model.SKU && x.ID != model.ID)
                                            .Any();

                    if (isSkuDuplicate)
                    {
                        ModelState.AddModelError("SKU", "Mã SKU này đã được dùng.");
                    }
                    else
                    {
                        // 3. CẬP NHẬT THÔNG TIN
                        var existItem = _unitOfWork.Repository<BienTheSanPham>().GetById(model.ID);

                        existItem.MauSacID = model.MauSacID;
                        existItem.KichThuocID = model.KichThuocID;
                        existItem.SoLuong = model.SoLuong;
                        existItem.SKU = model.SKU;

                        // 4. XỬ LÝ ẢNH (Nếu có upload mới)
                        if (ImageFile != null && ImageFile.ContentLength > 0)
                        {
                            string fileName = Path.GetFileName(ImageFile.FileName);
                            string newFileName = sanPham.Slug + "-color-" + model.MauSacID + "-" + DateTime.Now.Ticks + Path.GetExtension(fileName);
                            string path = Path.Combine(Server.MapPath("~/Content/images/sanpham/"), newFileName);
                            ImageFile.SaveAs(path);

                            // Tìm ảnh của màu này
                            var anhMau = _unitOfWork.Repository<AnhSanPham>()
                                            .GetMany(x => x.SanPhamID == model.SanPhamID && x.MauSacID == model.MauSacID)
                                            .FirstOrDefault();

                            if (anhMau != null)
                            {
                                // Update URL ảnh cũ
                                anhMau.URL = "/Content/images/sanpham/" + newFileName;
                                _unitOfWork.Repository<AnhSanPham>().Update(anhMau);
                            }
                            else
                            {
                                // Thêm mới
                                var newImg = new AnhSanPham()
                                {
                                    SanPhamID = model.SanPhamID,
                                    MauSacID = model.MauSacID,
                                    URL = "/Content/images/sanpham/" + newFileName,
                                    MacDinh = false
                                };
                                _unitOfWork.Repository<AnhSanPham>().Add(newImg);
                            }
                        }
                        // TH2: Người dùng CHỌN ẢNH CÓ SẴN
                        else if (SelectedImageId.HasValue)
                        {
                            var existingPhoto = _unitOfWork.Repository<AnhSanPham>().GetById(SelectedImageId.Value);
                            if (existingPhoto != null)
                            {
                                existingPhoto.MauSacID = model.MauSacID;
                                _unitOfWork.Repository<AnhSanPham>().Update(existingPhoto);
                            }
                        }

                        _unitOfWork.Repository<BienTheSanPham>().Update(existItem);
                        _unitOfWork.Save();

                        TempData["Message"] = "Cập nhật SKU thành công!";
                        return RedirectToAction("Index", new { sanPhamId = model.SanPhamID });
                    }
                }
            }

            ViewBag.SanPham = sanPham;
            ViewBag.MauSacID = new SelectList(_unitOfWork.Repository<MauSac>().GetAll(), "ID", "TenMau", model.MauSacID);
            ViewBag.KichThuocID = new SelectList(_unitOfWork.Repository<KichThuoc>().GetAll(), "ID", "TenSize", model.KichThuocID);
            ViewBag.ListAnhSanPham = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.SanPhamID == model.SanPhamID).ToList();
            return View(model);
        }





        // 4. XÓA BIẾN THỂ
        public ActionResult Delete(int id)
        {
            var sku = _unitOfWork.Repository<BienTheSanPham>().GetById(id);
            if (sku == null) return HttpNotFound();

            // Check nghiệp vụ: Đã có trong đơn hàng chưa?
            bool inOrder = _unitOfWork.Repository<ChiTietDonHang>().GetMany(x => x.BienTheSanPhamID == id).Any();
            if (inOrder)
            {
                TempData["Error"] = "Không thể xóa SKU này vì đã phát sinh đơn hàng.";
                return RedirectToAction("Index", new { sanPhamId = sku.SanPhamID });
            }

            _unitOfWork.Repository<BienTheSanPham>().Delete(id);
            _unitOfWork.Save();

            TempData["Message"] = "Đã xóa SKU thành công.";
            return RedirectToAction("Index", new { sanPhamId = sku.SanPhamID });
        }
    }
}