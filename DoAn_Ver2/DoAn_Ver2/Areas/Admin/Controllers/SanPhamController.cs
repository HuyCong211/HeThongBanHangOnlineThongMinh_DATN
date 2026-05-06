using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DoAn_Ver2.Models;
using DoAn_Ver2.Common;
using System.IO;
using PagedList;
using DoAn_Ver2.Models.AI_Services;
using System.Threading.Tasks;
using System.Net.Http;

namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class SanPhamController : BaseController
    {
        // GET: Admin/SanPham
        public ActionResult Index(string searchString, int? danhMucId, int? page, int? pageSize)
        {
            Session["CurrentSanPhamUrl"] = Request.Url.PathAndQuery;
            int pageNumber = (page ?? 1);
            int size = (pageSize ?? 10);
            ViewBag.PageSize = size;
            var query = _unitOfWork.Repository<SanPham>().GetAll().AsQueryable();
            if (danhMucId.HasValue)
            {
                var allCats = _unitOfWork.Repository<DanhMuc>().GetAll().ToList(); 
                var listCatIDs = GetChildCategoryIDs(allCats, danhMucId.Value);
                listCatIDs.Add(danhMucId.Value);
                query = query.Where(x => listCatIDs.Contains(x.DanhMucID ?? 0));
            }
            if (!string.IsNullOrEmpty(searchString))
            {
                string key = searchString.ToLower();
                query = query.Where(x => x.TenSanPham.ToLower().Contains(key) ||
                                         x.MaSanPham.ToLower().Contains(key) ||
                                         (x.Slug != null && x.Slug.Contains(key)));
            }
            query = query.OrderByDescending(x => x.ID);
            ViewBag.CurrentFilter = searchString;
            ViewBag.CurrentDanhMuc = danhMucId;
            var listDM = _unitOfWork.Repository<DanhMuc>().GetAll();
            ViewBag.ListDanhMuc = new SelectList(listDM, "ID", "TenDanhMuc", danhMucId);
            ViewBag.TenDanhMucMap = listDM.ToDictionary(x => x.ID, x => x.TenDanhMuc);
            var listAnh = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.MacDinh == true).ToList();
            ViewBag.AnhDaiDienMap = listAnh.ToDictionary(x => x.SanPhamID, x => x.URL);
            return View(query.ToPagedList(pageNumber, size));
        }

        // =============================================================
        // Hàm đệ quy lấy tất cả ID con cháu
        // =============================================================
        private List<int> GetChildCategoryIDs(List<DanhMuc> allCats, int parentId)
        {
            var childIDs = new List<int>();
            var children = allCats.Where(x => x.DanhMucChaID == parentId).Select(x => x.ID).ToList();

            foreach (var childId in children)
            {
                childIDs.Add(childId);
                childIDs.AddRange(GetChildCategoryIDs(allCats, childId));
            }

            return childIDs;
        }

        // GET: Admin/SanPham/Details/5
        public ActionResult Details(int id)
        {
            var model = _unitOfWork.Repository<SanPham>().GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }
            var danhMuc = _unitOfWork.Repository<DanhMuc>().GetById(model.DanhMucID);
            ViewBag.TenDanhMuc = danhMuc != null ? danhMuc.TenDanhMuc : "Không có";
            var allImages = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.SanPhamID == id).ToList();
            var anhChinh = allImages.FirstOrDefault(x => x.MacDinh == true);
            ViewBag.AnhChinh = anhChinh != null ? anhChinh.URL : "";
            ViewBag.AnhPhu = allImages.Where(x => x.MacDinh == false).ToList();
            var listSKU = _unitOfWork.Repository<BienTheSanPham>().GetMany(x => x.SanPhamID == id).ToList();
            ViewBag.ListSKU = listSKU;
            var listMau = _unitOfWork.Repository<MauSac>().GetAll();
            ViewBag.MauMap = listMau.ToDictionary(x => x.ID, x => x.TenMau);

            var listSize = _unitOfWork.Repository<KichThuoc>().GetAll();
            ViewBag.SizeMap = listSize.ToDictionary(x => x.ID, x => x.TenSize);
            return View(model);
        }


        // 2. TẠO MỚI (GET)
        public ActionResult Create()
        {
            ViewBag.DanhMucID = new SelectList(_unitOfWork.Repository<DanhMuc>().GetAll(), "ID", "TenDanhMuc");
            ViewBag.MauSacList = new SelectList(_unitOfWork.Repository<MauSac>().GetAll(), "ID", "TenMau");
            ViewBag.KichThuocList = new SelectList(_unitOfWork.Repository<KichThuoc>().GetAll(), "ID", "TenSize");
            ViewBag.AllTags = new List<string> { "Đi biển", "Đi làm", "Đi học", "Đi chơi", "Thể thao", "Dự tiệc", "Mặc nhà", "Dạo phố" };
            return View();
        }

        // 3. TẠO MỚI (POST)
        [HttpPost]
        [ValidateInput(false)] 
        [ValidateAntiForgeryToken]
        public ActionResult Create(SanPham model, HttpPostedFileBase ImageFile, List<HttpPostedFileBase> MoreImages, List<BienTheSanPham> BienThes, List<int?> ImageIndexes, List<string> KichThuocIDs)
        {
            if (!string.IsNullOrEmpty(model.MaSanPham))
            {
                bool isDupCode = _unitOfWork.Repository<SanPham>().GetAll().Any(x => x.MaSanPham.ToLower() == model.MaSanPham.ToLower());
                if (isDupCode) ModelState.AddModelError("MaSanPham", "Mã sản phẩm này đã tồn tại.");
            }
            if (!string.IsNullOrEmpty(model.TenSanPham))
            {
                bool isDupName = _unitOfWork.Repository<SanPham>().GetAll().Any(x => x.TenSanPham.ToLower() == model.TenSanPham.ToLower());
                if (isDupName) ModelState.AddModelError("TenSanPham", "Tên sản phẩm này đã tồn tại.");
            }
            List<BienTheSanPham> finalBienThes = new List<BienTheSanPham>();
            List<int?> finalImageIndexes = new List<int?>();
            if (BienThes != null && BienThes.Count > 0)
            {
                for (int i = 0; i < BienThes.Count; i++)
                {
                    var item = BienThes[i];
                    var imgIdx = (ImageIndexes != null && ImageIndexes.Count > i) ? ImageIndexes[i] : null;

                    string sizeIdsStr = (KichThuocIDs != null && KichThuocIDs.Count > i) ? KichThuocIDs[i] : null;

                    if (item.MauSacID > 0 && !string.IsNullOrEmpty(sizeIdsStr))
                    {
                        var listSizeId = sizeIdsStr.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).Select(int.Parse).ToList();

                        foreach (var sizeId in listSizeId)
                        {
                            BienTheSanPham newBt = new BienTheSanPham
                            {
                                MauSacID = item.MauSacID,
                                KichThuocID = sizeId,
                                SoLuong = item.SoLuong,
                            };
                            finalBienThes.Add(newBt);
                            finalImageIndexes.Add(imgIdx); 
                        }
                    }
                }
                var duplicateGroups = finalBienThes
                    .GroupBy(x => new { x.MauSacID, x.KichThuocID })
                    .Where(g => g.Count() > 1)
                    .ToList();

                if (duplicateGroups.Any())
                {
                    ModelState.AddModelError("", "Lỗi: Có các dòng biến thể trùng nhau (cùng Màu và Size). Vui lòng kiểm tra lại.");
                }
            }


            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(model.Slug)) model.Slug = MyTools.GenerateSlug(model.TenSanPham);
                if (string.IsNullOrEmpty(model.MaSanPham)) model.MaSanPham = "SP" + DateTime.Now.ToString("HHmmss");

                model.NgayTao = DateTime.Now;
                model.NgayCapNhat = DateTime.Now;
                model.LuotXem = 0;

                _unitOfWork.Repository<SanPham>().Add(model);
                _unitOfWork.Save();
                AnhSanPham savedMainImage = null;
                if (ImageFile != null && ImageFile.ContentLength > 0)
                {
                    string fileName = Path.GetFileName(ImageFile.FileName);
                    string newFileName = model.Slug + "-main-" + DateTime.Now.Ticks + Path.GetExtension(fileName);
                    string path = Path.Combine(Server.MapPath("~/Content/images/sanpham/"), newFileName);
                    if (!Directory.Exists(Server.MapPath("~/Content/images/sanpham/"))) Directory.CreateDirectory(Server.MapPath("~/Content/images/sanpham/"));
                    ImageFile.SaveAs(path);
                    var anh = new AnhSanPham() { SanPhamID = model.ID, URL = "/Content/images/sanpham/" + newFileName, MacDinh = true };
                    _unitOfWork.Repository<AnhSanPham>().Add(anh);
                    savedMainImage = anh;

                    // =========================================================
                    // ĐỒNG BỘ ẢNH CHÍNH LÊN PYTHON AI NGAY LẬP TỨC
                    // =========================================================
                    Task.Run(() => SyncImageToAIAync(path, newFileName));
                }
                List<AnhSanPham> savedMoreImages = new List<AnhSanPham>();
                if (MoreImages != null && MoreImages.Count > 0)
                {
                    foreach (var item in MoreImages)
                    {
                        if (item != null && item.ContentLength > 0)
                        {
                            string fileName = Path.GetFileName(item.FileName);
                            string newFileName = model.Slug + "-" + Guid.NewGuid().ToString().Substring(0, 6) + Path.GetExtension(fileName);
                            string path = Path.Combine(Server.MapPath("~/Content/images/sanpham/"), newFileName);
                            item.SaveAs(path);
                            var anh = new AnhSanPham() { SanPhamID = model.ID, URL = "/Content/images/sanpham/" + newFileName, MacDinh = false };
                            _unitOfWork.Repository<AnhSanPham>().Add(anh);
                            savedMoreImages.Add(anh);
                            // =========================================================
                            // ĐỒNG BỘ CẢ ẢNH PHỤ LÊN PYTHON AI
                            // =========================================================
                            Task.Run(() => SyncImageToAIAync(path, newFileName));
                        }
                        else
                        {
                            savedMoreImages.Add(null); 
                        }
                    }
                }
                _unitOfWork.Save();
                if (finalBienThes.Count > 0)
                {
                    var listMauDB = _unitOfWork.Repository<MauSac>().GetAll().ToList();
                    var listSizeDB = _unitOfWork.Repository<KichThuoc>().GetAll().ToList();
                    HashSet<int> processedColorImages = new HashSet<int>();

                    for (int i = 0; i < finalBienThes.Count; i++)
                    {
                        var item = finalBienThes[i];
                        item.SanPhamID = model.ID;
                        item.SoLuongTamGiu = 0;

                        var mauObj = listMauDB.FirstOrDefault(m => m.ID == item.MauSacID);
                        var sizeObj = listSizeDB.FirstOrDefault(s => s.ID == item.KichThuocID);

                        string tenMauCode = mauObj != null ? MyTools.GenerateSlug(mauObj.TenMau).ToUpper() : item.MauSacID.ToString();
                        string tenSizeCode = sizeObj != null ? sizeObj.TenSize.ToUpper() : item.KichThuocID.ToString();

                        item.SKU = $"{model.MaSanPham}-{tenMauCode}-{tenSizeCode}";

                        bool existSku = _unitOfWork.Repository<BienTheSanPham>().GetMany(x => x.SKU == item.SKU).Any();
                        if (existSku)
                        {
                            item.SKU = item.SKU + "-" + DateTime.Now.Ticks.ToString().Substring(10);
                        }

                        _unitOfWork.Repository<BienTheSanPham>().Add(item);
                        var imgIdx = finalImageIndexes[i];
                        if (imgIdx.HasValue)
                        {
                            int indexVal = imgIdx.Value;
                            if (indexVal == -1 && savedMainImage != null)
                            {
                                if (!processedColorImages.Contains(item.MauSacID))
                                {
                                    savedMainImage.MauSacID = item.MauSacID;
                                    _unitOfWork.Repository<AnhSanPham>().Update(savedMainImage);
                                    processedColorImages.Add(item.MauSacID);
                                }
                            }
                            else if (indexVal >= 0 && indexVal < savedMoreImages.Count && savedMoreImages[indexVal] != null)
                            {
                                if (!processedColorImages.Contains(item.MauSacID))
                                {
                                    var anhCanUpdate = savedMoreImages[indexVal];
                                    anhCanUpdate.MauSacID = item.MauSacID;
                                    _unitOfWork.Repository<AnhSanPham>().Update(anhCanUpdate);
                                    processedColorImages.Add(item.MauSacID);
                                }
                            }
                        }
                    }
                }

                _unitOfWork.Save();

                // =========================================================================
                // ĐỒNG BỘ SẢN PHẨM NÀY LÊN NÃO BỘ AI
                // =========================================================================
                Task.Run(async () =>
                {
                    try
                    {
                        string textData = $"[ID: {model.ID}] Tên sản phẩm: {model.TenSanPham}. " +
                                          $"Mô tả: {model.MoTa}. " +
                                          $"Giá bán: {model.GiaBan} VNĐ." +
                                          (!string.IsNullOrEmpty(model.Tags) ? $" Phù hợp cho: {model.Tags.Replace(",", ", ")}." : "");

                        CohereService cohere = new CohereService();
                        PineconeService pinecone = new PineconeService();

                        var vector = await cohere.GetEmbeddingAsync(textData);
                        await pinecone.UpsertVectorAsync(model.ID.ToString(), vector);

                        System.Diagnostics.Debug.WriteLine($"[AI SYNC] Đã đồng bộ thêm mới SP ID: {model.ID}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AI SYNC LỖI] {ex.Message}");
                    }
                });

                return RedirectToCurrentIndex();
            }
            ViewBag.DanhMucID = new SelectList(_unitOfWork.Repository<DanhMuc>().GetAll(), "ID", "TenDanhMuc", model.DanhMucID);
            ViewBag.MauSacList = new SelectList(_unitOfWork.Repository<MauSac>().GetAll(), "ID", "TenMau");
            ViewBag.KichThuocList = new SelectList(_unitOfWork.Repository<KichThuoc>().GetAll(), "ID", "TenSize");
            ViewBag.OldBienThes = BienThes; 
            ViewBag.OldImageIndexes = ImageIndexes;
            ViewBag.OldKichThuocIDs = KichThuocIDs;
            // THÊM DÒNG NÀY VÀO ĐỂ FIX LỖI:
            ViewBag.AllTags = new List<string> { "Đi biển", "Đi làm", "Đi học", "Đi chơi", "Thể thao", "Dự tiệc", "Mặc nhà", "Dạo phố" };

            return View(model);
        }


        // ==========================================================
        // CHỨC NĂNG 2: CẬP NHẬT (EDIT)
        // ==========================================================

        // GET: Hiển thị form sửa
        public ActionResult Edit(int id)
        {
            var model = _unitOfWork.Repository<SanPham>().GetById(id);
            if (model == null) return HttpNotFound();

            ViewBag.DanhMucID = new SelectList(_unitOfWork.Repository<DanhMuc>().GetAll(), "ID", "TenDanhMuc", model.DanhMucID);
            var anhChinh = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.SanPhamID == id && x.MacDinh == true).FirstOrDefault();
            ViewBag.AnhChinh = anhChinh != null ? anhChinh.URL : "";
            ViewBag.AnhPhu = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.SanPhamID == id && x.MacDinh == false).ToList();
            ViewBag.AllTags = new List<string> { "Đi biển", "Đi làm", "Đi học", "Đi chơi", "Thể thao", "Dự tiệc", "Mặc nhà", "Dạo phố" };

            return View(model);
        }

        // POST: Xử lý cập nhật
        [HttpPost]
        [ValidateInput(false)]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(SanPham model, HttpPostedFileBase ImageFile, List<HttpPostedFileBase> MoreImages)
        {
            // 1. VALIDATION CHECK TRÙNG (Trừ chính nó ra)
            if (!string.IsNullOrEmpty(model.MaSanPham))
            {
                bool isDupCode = _unitOfWork.Repository<SanPham>().GetAll()
                    .Any(x => x.MaSanPham.ToLower() == model.MaSanPham.ToLower() && x.ID != model.ID);
                if (isDupCode) ModelState.AddModelError("MaSanPham", "Mã sản phẩm đã được sử dụng bởi SP khác.");
            }

            if (!string.IsNullOrEmpty(model.TenSanPham))
            {
                bool isDupName = _unitOfWork.Repository<SanPham>().GetAll()
                    .Any(x => x.TenSanPham.ToLower() == model.TenSanPham.ToLower() && x.ID != model.ID);
                if (isDupName) ModelState.AddModelError("TenSanPham", "Tên sản phẩm đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                var existItem = _unitOfWork.Repository<SanPham>().GetById(model.ID);
                if (existItem == null) return HttpNotFound();
                existItem.TenSanPham = model.TenSanPham;
                existItem.Slug = MyTools.GenerateSlug(model.TenSanPham);
                existItem.MaSanPham = model.MaSanPham;
                existItem.DanhMucID = model.DanhMucID;
                existItem.GiaGoc = model.GiaGoc;
                existItem.GiaBan = model.GiaBan;
                existItem.MoTa = model.MoTa;
                existItem.ChatLieu = model.ChatLieu;
                existItem.KieuDang = model.KieuDang;
                existItem.TrangThai = model.TrangThai;
                existItem.Tags = model.Tags;
                existItem.NgayCapNhat = DateTime.Now;
                if (ImageFile != null && ImageFile.ContentLength > 0)
                {
                    var oldMain = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.SanPhamID == model.ID && x.MacDinh == true).FirstOrDefault();
                    string newFileName = existItem.Slug + "-main-" + DateTime.Now.Ticks + Path.GetExtension(ImageFile.FileName);
                    string path = Path.Combine(Server.MapPath("~/Content/images/sanpham/"), newFileName);
                    ImageFile.SaveAs(path);

                    if (oldMain != null)
                    {
                        oldMain.URL = "/Content/images/sanpham/" + newFileName;
                        _unitOfWork.Repository<AnhSanPham>().Update(oldMain);
                    }
                    else
                    {
                        var newImg = new AnhSanPham() { SanPhamID = model.ID, URL = "/Content/images/sanpham/" + newFileName, MacDinh = true };
                        _unitOfWork.Repository<AnhSanPham>().Add(newImg);
                    }
                }
                if (MoreImages != null && MoreImages.Count > 0)
                {
                    foreach (var item in MoreImages)
                    {
                        if (item != null && item.ContentLength > 0)
                        {
                            string newFileName = existItem.Slug + "-" + Guid.NewGuid().ToString().Substring(0, 6) + Path.GetExtension(item.FileName);
                            string path = Path.Combine(Server.MapPath("~/Content/images/sanpham/"), newFileName);
                            item.SaveAs(path);
                            var anh = new AnhSanPham() { SanPhamID = model.ID, URL = "/Content/images/sanpham/" + newFileName, MacDinh = false };
                            _unitOfWork.Repository<AnhSanPham>().Add(anh);
                        }
                    }
                }

                _unitOfWork.Repository<SanPham>().Update(existItem);
                _unitOfWork.Save();


                // =========================================================================
                // ĐỒNG BỘ SẢN PHẨM NÀY LÊN NÃO BỘ AI (CHẠY NGẦM)
                // =========================================================================
                Task.Run(async () =>
                {
                    try
                    {
                        string textData = $"[ID: {existItem.ID}] Tên sản phẩm: {existItem.TenSanPham}. " +
                                          $"Mô tả: {existItem.MoTa}. " +
                                          $"Giá bán: {existItem.GiaBan} VNĐ." +
                                          (!string.IsNullOrEmpty(existItem.Tags) ? $" Phù hợp cho: {existItem.Tags.Replace(",", ", ")}." : "");

                        CohereService cohere = new CohereService();
                        PineconeService pinecone = new PineconeService();

                        var vector = await cohere.GetEmbeddingAsync(textData);
                        await pinecone.UpsertVectorAsync(existItem.ID.ToString(), vector);

                        System.Diagnostics.Debug.WriteLine($"[AI SYNC] Đã đồng bộ cập nhật SP ID: {existItem.ID}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AI SYNC LỖI] {ex.Message}");
                    }
                });
                // =========================================================================

                return RedirectToCurrentIndex();
            }
            ViewBag.DanhMucID = new SelectList(_unitOfWork.Repository<DanhMuc>().GetAll(), "ID", "TenDanhMuc", model.DanhMucID);
            var aChinh = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.SanPhamID == model.ID && x.MacDinh == true).FirstOrDefault();
            ViewBag.AnhChinh = aChinh != null ? aChinh.URL : "";
            ViewBag.AnhPhu = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.SanPhamID == model.ID && x.MacDinh == false).ToList();

            return View(model);
        }


        // ==========================================
        // AJAX: XÓA ẢNH PHỤ TRONG DB
        // ==========================================
        [HttpPost]
        public JsonResult DeleteImage(int id)
        {
            try
            {
                var anh = _unitOfWork.Repository<AnhSanPham>().GetById(id);
                if (anh == null) return Json(new { success = false, message = "Không tìm thấy ảnh" });
                if (anh.MacDinh == true) return Json(new { success = false, message = "Không thể xóa ảnh chính" });

                _unitOfWork.Repository<AnhSanPham>().Delete(anh);
                _unitOfWork.Save();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ======================
        // XÓA SẢN PHẨM (DELETE)
        // ======================

        // 1. GET: Hiển thị trang xác nhận xóa
        public ActionResult Delete(int id)
        {
            var model = _unitOfWork.Repository<SanPham>().GetById(id);
            if (model == null) return HttpNotFound();

            // KIỂM TRA NGHIỆP VỤ: Sản phẩm đã phát sinh đơn hàng chưa?
            bool daPhatSinhDonHang = false;
            var listBienTheID = _unitOfWork.Repository<BienTheSanPham>()
                                    .GetMany(x => x.SanPhamID == id)
                                    .Select(x => x.ID).ToList();
            if (listBienTheID.Count > 0)
            {
                daPhatSinhDonHang = _unitOfWork.Repository<ChiTietDonHang>()
                                        .GetMany(x => listBienTheID.Contains(x.BienTheSanPhamID))
                                        .Any();
            }
            ViewBag.DaPhatSinhDonHang = daPhatSinhDonHang;

            var anhChinh = _unitOfWork.Repository<AnhSanPham>()
                        .GetMany(x => x.SanPhamID == id && x.MacDinh == true)
                        .FirstOrDefault();
            ViewBag.AnhDaiDien = anhChinh != null ? anhChinh.URL : "";

            return View(model);
        }

        // 2. POST: Xử lý Xóa vĩnh viễn 
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var model = _unitOfWork.Repository<SanPham>().GetById(id);
            if (model == null) return HttpNotFound();

            try
            {
                var listAnh = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.SanPhamID == id).ToList();
                foreach (var anh in listAnh)
                {
                    _unitOfWork.Repository<AnhSanPham>().Delete(anh);
                }
                var listSKU = _unitOfWork.Repository<BienTheSanPham>().GetMany(x => x.SanPhamID == id).ToList();
                foreach (var sku in listSKU)
                {
                    _unitOfWork.Repository<BienTheSanPham>().Delete(sku);
                }
                _unitOfWork.Repository<SanPham>().Delete(model);
                _unitOfWork.Save();

                return RedirectToCurrentIndex();
            }
            catch (Exception)
            {
                ViewBag.Error = "Không thể xóa sản phẩm này do ràng buộc dữ liệu phức tạp.";
                ViewBag.DaPhatSinhDonHang = true; 
                return View(model);
            }
        }

        // 3. POST: Chuyển trạng thái sang Ngừng Kinh Doanh (Soft Delete)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Deactive(int id)
        {
            var model = _unitOfWork.Repository<SanPham>().GetById(id);
            if (model != null)
            {
                model.TrangThai = 0; 
                model.NgayCapNhat = DateTime.Now;

                _unitOfWork.Repository<SanPham>().Update(model);
                _unitOfWork.Save();
            }
            return RedirectToCurrentIndex();
        }


        // AJAX: Thêm nhanh Màu sắc 
        [HttpPost]
        public JsonResult AjaxCreateMau(string tenMau, string maHex)
        {
            if (string.IsNullOrWhiteSpace(tenMau))
                return Json(new { success = false, message = "Vui lòng nhập tên màu." });
            if (string.IsNullOrWhiteSpace(maHex))
                maHex = "#000000";
            if (_unitOfWork.Repository<MauSac>().GetAll().Any(x => x.TenMau.ToLower() == tenMau.ToLower()))
                return Json(new { success = false, message = "Tên màu này đã tồn tại." });

            try
            {
                var mau = new MauSac
                {
                    TenMau = tenMau,
                    MaHex = maHex
                };

                _unitOfWork.Repository<MauSac>().Add(mau);
                _unitOfWork.Save();

                return Json(new { success = true, id = mau.ID, name = mau.TenMau });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // AJAX: Thêm nhanh Kích thước
        [HttpPost]
        public JsonResult AjaxCreateSize(string tenSize)
        {
            if (string.IsNullOrWhiteSpace(tenSize))
                return Json(new { success = false, message = "Tên size không được để trống." });

            if (_unitOfWork.Repository<KichThuoc>().GetAll().Any(x => x.TenSize.ToLower() == tenSize.ToLower()))
                return Json(new { success = false, message = "Size này đã tồn tại." });

            try
            {
                var size = new KichThuoc { TenSize = tenSize };
                _unitOfWork.Repository<KichThuoc>().Add(size);
                _unitOfWork.Save();
                return Json(new { success = true, id = size.ID, name = size.TenSize });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private ActionResult RedirectToCurrentIndex()
        {
            string returnUrl = Session["CurrentSanPhamUrl"] as string;
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl); 
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult BackToIndex()
        {
            return RedirectToCurrentIndex();
        }


        // ==========================================
        // HELPER: ĐỒNG BỘ ẢNH SANG MICROSERVICE AI
        // ==========================================
        private async Task SyncImageToAIAync(string physicalFilePath, string fileName)
        {
            try
            {
                if (!System.IO.File.Exists(physicalFilePath)) return;

                using (var client = new HttpClient())
                {
                    using (var fileStream = new FileStream(physicalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var content = new MultipartFormDataContent();
                        content.Add(new StreamContent(fileStream), "file", fileName);
                        content.Add(new StringContent(fileName), "image_name"); 
                        client.Timeout = TimeSpan.FromSeconds(10);
                        await client.PostAsync("http://127.0.0.1:5000/add_index", content);
                        System.Diagnostics.Debug.WriteLine($"[AI IMAGE SYNC] Đồng bộ thành công: {fileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI IMAGE SYNC LỖI] {ex.Message}");
            }
        }

    }
}