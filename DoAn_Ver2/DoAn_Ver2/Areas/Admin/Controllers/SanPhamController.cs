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

            // 1. Tối ưu truy vấn: Sử dụng AsQueryable để chưa thực thi SQL ngay
            var query = _unitOfWork.Repository<SanPham>().GetAll().AsQueryable();

            // 2. Lọc theo Danh mục
            if (danhMucId.HasValue)
            {
                // 1. Lấy TOÀN BỘ danh mục về List (Tải về RAM 1 lần duy nhất)
                var allCats = _unitOfWork.Repository<DanhMuc>().GetAll().ToList(); // <-- THÊM .ToList() Ở ĐÂY

                // 2. Tìm tất cả ID con cháu
                var listCatIDs = GetChildCategoryIDs(allCats, danhMucId.Value);

                // 3. Thêm chính nó
                listCatIDs.Add(danhMucId.Value);

                // 4. Lọc
                query = query.Where(x => listCatIDs.Contains(x.DanhMucID ?? 0));
            }

            // 3. Tìm kiếm đa năng (Tên, Mã, Slug)
            if (!string.IsNullOrEmpty(searchString))
            {
                string key = searchString.ToLower();
                query = query.Where(x => x.TenSanPham.ToLower().Contains(key) ||
                                         x.MaSanPham.ToLower().Contains(key) ||
                                         (x.Slug != null && x.Slug.Contains(key)));
            }

            // 4. Sắp xếp (Mới nhất lên đầu)
            query = query.OrderByDescending(x => x.ID);

            // 5. Chuẩn bị dữ liệu cho View
            ViewBag.CurrentFilter = searchString;
            ViewBag.CurrentDanhMuc = danhMucId;

            // Load danh mục cho Dropdown lọc (bao gồm cả danh mục cha/con nếu cần logic phức tạp hơn)
            var listDM = _unitOfWork.Repository<DanhMuc>().GetAll();
            ViewBag.ListDanhMuc = new SelectList(listDM, "ID", "TenDanhMuc", danhMucId);

            // Tạo Dictionary để hiển thị Tên Danh Mục nhanh (tránh lỗi lazy loading null)
            ViewBag.TenDanhMucMap = listDM.ToDictionary(x => x.ID, x => x.TenDanhMuc);

            // 3. Load Ảnh Đại Diện (Map SanPhamID -> URL) - GIẢI QUYẾT VẤN ĐỀ ẢNH KHÔNG HIỆN
            // Lấy tất cả ảnh mặc định
            var listAnh = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.MacDinh == true).ToList();
            // Tạo Dictionary: Key=SanPhamID, Value=URL
            ViewBag.AnhDaiDienMap = listAnh.ToDictionary(x => x.SanPhamID, x => x.URL);

            // 6. Thực thi phân trang (Lúc này SQL mới chạy LIMIT/OFFSET -> Nhanh)
            return View(query.ToPagedList(pageNumber, size));
        }

        // =============================================================
        // HELPER: Hàm đệ quy lấy tất cả ID con cháu
        // =============================================================
        private List<int> GetChildCategoryIDs(List<DanhMuc> allCats, int parentId)
        {
            var childIDs = new List<int>();

            // Tìm các danh mục con trực tiếp của parentId
            var children = allCats.Where(x => x.DanhMucChaID == parentId).Select(x => x.ID).ToList();

            foreach (var childId in children)
            {
                childIDs.Add(childId);
                // Đệ quy: Tìm tiếp con của con
                childIDs.AddRange(GetChildCategoryIDs(allCats, childId));
            }

            return childIDs;
        }

        // GET: Admin/SanPham/Details/5
        public ActionResult Details(int id)
        {
            // 1. Lấy sản phẩm
            var model = _unitOfWork.Repository<SanPham>().GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            // 2. Lấy tên danh mục (Vì lazy loading tắt nên ta query thủ công cho chắc)
            var danhMuc = _unitOfWork.Repository<DanhMuc>().GetById(model.DanhMucID);
            ViewBag.TenDanhMuc = danhMuc != null ? danhMuc.TenDanhMuc : "Không có";

            // 3. Lấy danh sách ảnh
            var allImages = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.SanPhamID == id).ToList();

            // Tách ảnh chính
            var anhChinh = allImages.FirstOrDefault(x => x.MacDinh == true);
            ViewBag.AnhChinh = anhChinh != null ? anhChinh.URL : "";

            // Tách ảnh phụ
            ViewBag.AnhPhu = allImages.Where(x => x.MacDinh == false).ToList();
            // --- MỚI: Lấy danh sách biến thể (SKU) ---
            // Cần Include hoặc Join để lấy tên Màu và Tên Size nếu model BienTheSanPham có navigation property
            // Nếu Repository của bạn hỗ trợ .Include("MauSac").Include("KichThuoc") thì dùng.
            // Nếu không, ta phải query thủ công hoặc dùng View để hiển thị.
            // Giả sử ta lấy list về và xử lý hiển thị ở View.
            var listSKU = _unitOfWork.Repository<BienTheSanPham>().GetMany(x => x.SanPhamID == id).ToList();
            ViewBag.ListSKU = listSKU;

            // Lấy Map tên Màu và Size để hiển thị nhanh (tránh query n+1 trong View)
            var listMau = _unitOfWork.Repository<MauSac>().GetAll();
            ViewBag.MauMap = listMau.ToDictionary(x => x.ID, x => x.TenMau);

            var listSize = _unitOfWork.Repository<KichThuoc>().GetAll();
            ViewBag.SizeMap = listSize.ToDictionary(x => x.ID, x => x.TenSize);
            return View(model);
        }


        // 2. TẠO MỚI (GET)
        public ActionResult Create()
        {
            // Chuẩn bị Dropdown Danh mục
            ViewBag.DanhMucID = new SelectList(_unitOfWork.Repository<DanhMuc>().GetAll(), "ID", "TenDanhMuc");
            // --- MỚI: Load dữ liệu cho Dropdown Biến thể (Màu, Size) ---
            ViewBag.MauSacList = new SelectList(_unitOfWork.Repository<MauSac>().GetAll(), "ID", "TenMau");
            ViewBag.KichThuocList = new SelectList(_unitOfWork.Repository<KichThuoc>().GetAll(), "ID", "TenSize");
            ViewBag.AllTags = new List<string> { "Đi biển", "Đi làm", "Đi học", "Đi chơi", "Thể thao", "Dự tiệc", "Mặc nhà", "Dạo phố" };
            // -----------------------------------------------------------
            return View();
        }

        // 3. TẠO MỚI (POST)
        [HttpPost]
        [ValidateInput(false)] // Quan trọng: Cho phép gửi mã HTML từ CKEditor
        [ValidateAntiForgeryToken]
        public ActionResult Create(SanPham model, HttpPostedFileBase ImageFile, List<HttpPostedFileBase> MoreImages, List<BienTheSanPham> BienThes, List<int?> ImageIndexes, List<string> KichThuocIDs)
        {
            // 1. VALIDATION THỦ CÔNG (Check trùng lặp)
            // Check trùng Mã sản phẩm (nếu người dùng tự nhập)
            if (!string.IsNullOrEmpty(model.MaSanPham))
            {
                bool isDupCode = _unitOfWork.Repository<SanPham>().GetAll().Any(x => x.MaSanPham.ToLower() == model.MaSanPham.ToLower());
                if (isDupCode) ModelState.AddModelError("MaSanPham", "Mã sản phẩm này đã tồn tại.");
            }

            // Check trùng Tên sản phẩm
            if (!string.IsNullOrEmpty(model.TenSanPham))
            {
                bool isDupName = _unitOfWork.Repository<SanPham>().GetAll().Any(x => x.TenSanPham.ToLower() == model.TenSanPham.ToLower());
                if (isDupName) ModelState.AddModelError("TenSanPham", "Tên sản phẩm này đã tồn tại.");
            }


            // --- VALIDATION TRÙNG LẶP BIẾN THỂ TRONG DANH SÁCH GỬI LÊN ---
            // Chúng ta sẽ validation sau khi đã bóc tách được danh sách biến thể thực sự
            List<BienTheSanPham> finalBienThes = new List<BienTheSanPham>();
            List<int?> finalImageIndexes = new List<int?>();
            if (BienThes != null && BienThes.Count > 0)
            {
                for (int i = 0; i < BienThes.Count; i++)
                {
                    var item = BienThes[i];
                    var imgIdx = (ImageIndexes != null && ImageIndexes.Count > i) ? ImageIndexes[i] : null;

                    // Lấy chuỗi các Size ID tương ứng với dòng màu này (ví dụ "1,2,3")
                    string sizeIdsStr = (KichThuocIDs != null && KichThuocIDs.Count > i) ? KichThuocIDs[i] : null;

                    if (item.MauSacID > 0 && !string.IsNullOrEmpty(sizeIdsStr))
                    {
                        // Tách chuỗi ra thành mảng ID
                        var listSizeId = sizeIdsStr.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).Select(int.Parse).ToList();

                        foreach (var sizeId in listSizeId)
                        {
                            // Tạo bản sao của item cho từng size
                            BienTheSanPham newBt = new BienTheSanPham
                            {
                                MauSacID = item.MauSacID,
                                KichThuocID = sizeId,
                                SoLuong = item.SoLuong, // Hoặc SoLuongTamGiu, v.v. tùy model
                                // Sao chép các thuộc tính khác nếu có
                            };
                            finalBienThes.Add(newBt);
                            finalImageIndexes.Add(imgIdx); // Cùng chung 1 ảnh index (ảnh của màu)
                        }
                    }
                }

                // Check trùng lặp trên finalBienThes
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
                // Logic tạo Slug & Mã tự động nếu để trống
                if (string.IsNullOrEmpty(model.Slug)) model.Slug = MyTools.GenerateSlug(model.TenSanPham);
                if (string.IsNullOrEmpty(model.MaSanPham)) model.MaSanPham = "SP" + DateTime.Now.ToString("HHmmss");

                model.NgayTao = DateTime.Now;
                model.NgayCapNhat = DateTime.Now;
                model.LuotXem = 0;

                _unitOfWork.Repository<SanPham>().Add(model);
                _unitOfWork.Save();

                // Biến lưu ảnh đại diện chính để dùng cho việc map màu sau này
                AnhSanPham savedMainImage = null;

                // ... (Logic Lưu ảnh chính giữ nguyên) ...
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
                }

                // 3. Lưu Ảnh Phụ & Giữ lại danh sách
                List<AnhSanPham> savedMoreImages = new List<AnhSanPham>();

                // Logic lưu ảnh phụ giữ nguyên...
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
                        }
                        else
                        {
                            savedMoreImages.Add(null); // Giữ chỗ cho index
                        }
                    }
                }

                // Lưu DB để có ID ảnh
                _unitOfWork.Save();

                // --- 4. MỚI: LƯU BIẾN THỂ (NẾU CÓ) ---
                // 4. Lưu Biến Thể & Map Ảnh
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

                        // Map ảnh chỉ map 1 lần cho mỗi màu
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
                // [THÊM MỚI] ĐỒNG BỘ SẢN PHẨM NÀY LÊN NÃO BỘ AI (CHẠY NGẦM)
                // =========================================================================
                Task.Run(async () =>
                {
                    try
                    {
                        // Lấy dữ liệu sản phẩm vừa lưu
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

            // Nếu lỗi: Load lại dropdown
            ViewBag.DanhMucID = new SelectList(_unitOfWork.Repository<DanhMuc>().GetAll(), "ID", "TenDanhMuc", model.DanhMucID);
            ViewBag.MauSacList = new SelectList(_unitOfWork.Repository<MauSac>().GetAll(), "ID", "TenMau");
            ViewBag.KichThuocList = new SelectList(_unitOfWork.Repository<KichThuoc>().GetAll(), "ID", "TenSize");

            // TRẢ VỀ VIEW KÈM DỮ LIỆU ĐỂ KHÔNG MẤT DÒNG NHẬP
            ViewBag.OldBienThes = BienThes; // Truyền lại danh sách biến thể đã nhập để render lại
            ViewBag.OldImageIndexes = ImageIndexes;
            ViewBag.OldKichThuocIDs = KichThuocIDs;
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

            // Tách riêng ảnh chính và list ảnh phụ để View dễ xử lý
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

                // Update text
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

                // ... (Logic Cập nhật ảnh đại diện giữ nguyên) ...
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

                // ... (Logic Thêm ảnh phụ giữ nguyên) ...
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
                // [CẬP NHẬT] ĐỒNG BỘ SẢN PHẨM NÀY LÊN NÃO BỘ AI (CHẠY NGẦM)
                // =========================================================================
                Task.Run(async () =>
                {
                    try
                    {
                        // Lấy dữ liệu sản phẩm vừa cập nhật
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

            // Nếu lỗi validate, load lại dữ liệu để trả về View
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

                // LƯU Ý: Không xóa file trên ổ cứng theo yêu cầu
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ==========================================
        // XÓA SẢN PHẨM (DELETE)
        // ==========================================

        // 1. GET: Hiển thị trang xác nhận xóa
        public ActionResult Delete(int id)
        {
            var model = _unitOfWork.Repository<SanPham>().GetById(id);
            if (model == null) return HttpNotFound();

            // KIỂM TRA NGHIỆP VỤ: Sản phẩm đã phát sinh đơn hàng chưa?
            // Logic: Lấy tất cả SKU của SP này -> Check xem SKU đó có trong ChiTietDonHang không

            bool daPhatSinhDonHang = false;

            // B1: Lấy các ID biến thể của sản phẩm
            var listBienTheID = _unitOfWork.Repository<BienTheSanPham>()
                                    .GetMany(x => x.SanPhamID == id)
                                    .Select(x => x.ID).ToList();

            // B2: Nếu có biến thể, kiểm tra trong bảng ChiTietDonHang
            if (listBienTheID.Count > 0)
            {
                daPhatSinhDonHang = _unitOfWork.Repository<ChiTietDonHang>()
                                        .GetMany(x => listBienTheID.Contains(x.BienTheSanPhamID))
                                        .Any();
            }

            // Truyền cờ này sang View để quyết định hiện nút Xóa hay nút Ngừng KD
            ViewBag.DaPhatSinhDonHang = daPhatSinhDonHang;

            var anhChinh = _unitOfWork.Repository<AnhSanPham>()
                        .GetMany(x => x.SanPhamID == id && x.MacDinh == true)
                        .FirstOrDefault();
            ViewBag.AnhDaiDien = anhChinh != null ? anhChinh.URL : "";

            return View(model);
        }

        // 2. POST: Xử lý Xóa vĩnh viễn (Chỉ dùng khi chưa có đơn hàng)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var model = _unitOfWork.Repository<SanPham>().GetById(id);
            if (model == null) return HttpNotFound();

            try
            {
                // 1. Xóa các ảnh phụ trong bảng AnhSanPham trước (tránh lỗi FK)
                var listAnh = _unitOfWork.Repository<AnhSanPham>().GetMany(x => x.SanPhamID == id).ToList();
                foreach (var anh in listAnh)
                {
                    _unitOfWork.Repository<AnhSanPham>().Delete(anh);
                }

                // 2. Xóa các biến thể (SKU) nếu có (Chỉ xóa được nếu biến thể chưa có đơn hàng)
                var listSKU = _unitOfWork.Repository<BienTheSanPham>().GetMany(x => x.SanPhamID == id).ToList();
                foreach (var sku in listSKU)
                {
                    _unitOfWork.Repository<BienTheSanPham>().Delete(sku);
                }

                // 3. Xóa Sản phẩm
                _unitOfWork.Repository<SanPham>().Delete(model);
                _unitOfWork.Save();

                return RedirectToCurrentIndex();
            }
            catch (Exception)
            {
                // Nếu vẫn lỗi (do ràng buộc khác chưa tính hết), chuyển về trang thông báo
                ViewBag.Error = "Không thể xóa sản phẩm này do ràng buộc dữ liệu phức tạp.";
                ViewBag.DaPhatSinhDonHang = true; // Để hiện nút hủy
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
                model.TrangThai = 0; // 0: Ngừng kinh doanh
                model.NgayCapNhat = DateTime.Now;

                _unitOfWork.Repository<SanPham>().Update(model);
                _unitOfWork.Save();
            }
            return RedirectToCurrentIndex();
        }


        // AJAX: Thêm nhanh Màu sắc (Có lưu mã Hex)
        [HttpPost]
        public JsonResult AjaxCreateMau(string tenMau, string maHex)
        {
            if (string.IsNullOrWhiteSpace(tenMau))
                return Json(new { success = false, message = "Vui lòng nhập tên màu." });

            // Nếu người dùng không chọn màu, mặc định có thể để trống hoặc set màu đen/trắng tùy logic
            if (string.IsNullOrWhiteSpace(maHex))
                maHex = "#000000";

            // Check trùng tên
            if (_unitOfWork.Repository<MauSac>().GetAll().Any(x => x.TenMau.ToLower() == tenMau.ToLower()))
                return Json(new { success = false, message = "Tên màu này đã tồn tại." });

            try
            {
                var mau = new MauSac
                {
                    TenMau = tenMau,
                    MaHex = maHex // Lưu mã màu vào cột MaHex trong DB
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





        // [THÊM HÀM NÀY VÀO CUỐI CONTROLLER]
        private ActionResult RedirectToCurrentIndex()
        {
            string returnUrl = Session["CurrentSanPhamUrl"] as string;
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl); // Trở về đúng trang đã lưu kèm đầy đủ bộ lọc
            }
            return RedirectToAction("Index"); // Phòng hờ nếu Session rỗng thì về trang chủ mặc định
        }

        // [THÊM MỚI] Hàm dùng riêng cho các nút "Quay lại" trên giao diện
        [HttpGet]
        public ActionResult BackToIndex()
        {
            // Gọi lại hàm dùng chung đã viết ở bước trước
            return RedirectToCurrentIndex();
        }

    }
}