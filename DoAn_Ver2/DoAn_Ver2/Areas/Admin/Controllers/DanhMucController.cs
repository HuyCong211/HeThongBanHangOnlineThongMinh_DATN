using DoAn_Ver2.Common;        
using DoAn_Ver2.Models;       
using PagedList;
using System;
using System.Collections.Generic;
using System.Data.Entity;      
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;


namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class DanhMucController : BaseController
    {
        // GET: Admin/DanhMuc
        public ActionResult Index(string searchString, int? danhMucChaId, int? page, int? pageSize)
        {
            int pageNumber = (page ?? 1);
            int size = (pageSize ?? 10);
            ViewBag.PageSize = size;

            // 1. Lấy tất cả dữ liệu
            var allList = _unitOfWork.Repository<DanhMuc>().GetAll(); // Trả về List<DanhMuc>

            // --- ĐOẠN MỚI THÊM: TẠO DICTIONARY TRA CỨU TÊN ---
            // Tạo một từ điển: Key là ID, Value là TenDanhMuc
            // Dùng cái này để tra tên cha ở bên View cực nhanh
            ViewBag.TenDanhMucChaMap = allList.ToDictionary(x => x.ID, x => x.TenDanhMuc);
            // --------------------------------------------------

            IEnumerable<DanhMuc> query = allList;

            // 2. Tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(x => x.TenDanhMuc.ToLower().Contains(searchString.ToLower()) ||
                                         (x.Slug != null && x.Slug.Contains(searchString)));
            }

            // 3. Lọc theo danh mục cha
            if (danhMucChaId.HasValue)
            {
                query = query.Where(x => x.DanhMucChaID == danhMucChaId);
            }

            // 4. Sắp xếp
            query = query.OrderByDescending(x => x.ID);

            // 5. ViewBag cho Dropdown lọc
            ViewBag.CurrentFilter = searchString;
            ViewBag.CurrentParent = danhMucChaId;
            ViewBag.ListDanhMuc = new SelectList(allList, "ID", "TenDanhMuc", danhMucChaId);

            return View(query.ToPagedList(pageNumber, size));
        }


        // GET: Admin/DanhMuc/Details/5
        public ActionResult Details(int id)
        {
            // 1. Lấy thông tin danh mục hiện tại
            var model = _unitOfWork.Repository<DanhMuc>().GetById(id);

            if (model == null)
            {
                return HttpNotFound();
            }

            // 2. Lấy tên Danh mục Cha (Nếu có)
            if (model.DanhMucChaID.HasValue)
            {
                var parent = _unitOfWork.Repository<DanhMuc>().GetById(model.DanhMucChaID.Value);
                ViewBag.TenDanhMucCha = parent != null ? parent.TenDanhMuc : "Không xác định";
            }
            else
            {
                ViewBag.TenDanhMucCha = "Là danh mục gốc (Cấp 1)";
            }

            // 3. Lấy danh sách Danh mục Con trực tiếp
            var listCon = _unitOfWork.Repository<DanhMuc>()
                                     .GetAll()
                                     .Where(x => x.DanhMucChaID == id)
                                     .ToList();

            ViewBag.ListDanhMucCon = listCon;

            // --- 4. ĐẾM SẢN PHẨM (BAO GỒM CẢ CON CHÁU) ---
            // Lấy toàn bộ danh mục để tính toán cây phân cấp
            var allCats = _unitOfWork.Repository<DanhMuc>().GetAll().ToList();

            // Tìm tất cả ID con cháu của danh mục hiện tại
            var listAllSubIDs = GetChildCategoryIDs(allCats, id);

            // Thêm chính nó vào danh sách (để đếm cả SP thuộc chính nó)
            listAllSubIDs.Add(id);

            // Đếm sản phẩm có DanhMucID nằm trong danh sách này
            var soLuongSanPham = _unitOfWork.Repository<SanPham>()
                                            .GetAll()
                                            .Count(x => listAllSubIDs.Contains(x.DanhMucID ?? 0));

            ViewBag.SoLuongSanPham = soLuongSanPham;
            // ----------------------------------------------

            return View(model);
        }


        // 2. TẠO MỚI (Giao diện)
        public ActionResult Create()
        {
            ViewBag.ListDanhMuc = GetDanhMucSelectList();
            return View();
        }

        // 3. TẠO MỚI (Xử lý POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(DanhMuc model, HttpPostedFileBase ImageFile)
        {
            // 1. VALIDATION DỮ LIỆU

            // -- VALIDATION THỦ CÔNG (Nếu model annotation không bắt được) --
            if (string.IsNullOrWhiteSpace(model.TenDanhMuc))
            {
                ModelState.AddModelError("TenDanhMuc", "Tên danh mục không được để trống.");
            }
            // Check trùng tên
            if (_unitOfWork.Repository<DanhMuc>().GetAll().Any(x => x.TenDanhMuc.ToLower() == model.TenDanhMuc.ToLower()))
            {
                ModelState.AddModelError("TenDanhMuc", "Tên danh mục này đã tồn tại.");
            }


            // Validation ảnh
            if (ImageFile != null && ImageFile.ContentLength > 10 * 1024 * 1024)
            {
                ModelState.AddModelError("HinhAnh", "Ảnh quá lớn (Max 10MB).");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (string.IsNullOrEmpty(model.Slug))
                        model.Slug = MyTools.GenerateSlug(model.TenDanhMuc);

                    if (ImageFile != null && ImageFile.ContentLength > 0)
                    {
                        string ext = Path.GetExtension(ImageFile.FileName);
                        string newFileName = model.Slug + "-" + DateTime.Now.Ticks + ext;
                        string path = Path.Combine(Server.MapPath("~/Content/images/danhmuc/"), newFileName);

                        var folder = Server.MapPath("~/Content/images/danhmuc/");
                        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                        ImageFile.SaveAs(path);
                        model.HinhAnh = "/Content/images/danhmuc/" + newFileName;
                    }

                    model.NgayTao = DateTime.Now;

                    _unitOfWork.Repository<DanhMuc>().Add(model);
                    _unitOfWork.Save();

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Lỗi: " + ex.Message);
                }
            }

            // Load lại dropdown nếu lỗi
            ViewBag.ListDanhMuc = GetDanhMucSelectList(model.DanhMucChaID);
            return View(model);
        }

        // 4. CHỈNH SỬA (Giao diện)
        public ActionResult Edit(int id)
        {
            var model = _unitOfWork.Repository<DanhMuc>().GetById(id);
            if (model == null) return HttpNotFound();
            ViewBag.ListDanhMuc = GetDanhMucSelectList(model.DanhMucChaID, excludeId: id);
            return View(model);
        }

        // 5. CHỈNH SỬA (Xử lý POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(DanhMuc model, HttpPostedFileBase ImageFile)
        {
            if (string.IsNullOrWhiteSpace(model.TenDanhMuc))
            {
                ModelState.AddModelError("TenDanhMuc", "Tên danh mục không được để trống.");
            }
            // Check trùng tên (trừ chính nó)
            bool isDuplicate = _unitOfWork.Repository<DanhMuc>().GetAll()
                .Any(x => x.TenDanhMuc.ToLower() == model.TenDanhMuc.ToLower() && x.ID != model.ID);

            if (isDuplicate) ModelState.AddModelError("TenDanhMuc", "Tên danh mục đã tồn tại.");

            if (ModelState.IsValid)
            {
                var existItem = _unitOfWork.Repository<DanhMuc>().GetById(model.ID);
                if (existItem != null)
                {
                    existItem.TenDanhMuc = model.TenDanhMuc;
                    existItem.Slug = MyTools.GenerateSlug(model.TenDanhMuc);

                    // CẬP NHẬT DANH MỤC CHA
                    existItem.DanhMucChaID = model.DanhMucChaID;
                    existItem.MoTa = model.MoTa;
                    existItem.NgayCapNhat = DateTime.Now;

                    if (ImageFile != null && ImageFile.ContentLength > 0)
                    {
                        string ext = Path.GetExtension(ImageFile.FileName);
                        string newFileName = existItem.Slug + "-" + DateTime.Now.Ticks + ext;
                        string path = Path.Combine(Server.MapPath("~/Content/images/danhmuc/"), newFileName);
                        ImageFile.SaveAs(path);
                        existItem.HinhAnh = "/Content/images/danhmuc/" + newFileName;
                    }

                    _unitOfWork.Repository<DanhMuc>().Update(existItem);
                    _unitOfWork.Save();
                    return RedirectToAction("Index");
                }
            }

            ViewBag.ListDanhMuc = GetDanhMucSelectList(model.DanhMucChaID, excludeId: model.ID);
            return View(model);
        }

        // 6. XÓA (Nên dùng Ajax hoặc Post để bảo mật, ở đây làm Get đơn giản trước)
        //xóa nhanh
        //public ActionResult Delete(int id)
        //{
        //    _unitOfWork.Repository<DanhMuc>().Delete(id);
        //    _unitOfWork.Save();
        //    return RedirectToAction("Index");
        //}

        //XEM TRƯỚC KHI XÓA
        // 1. GET: Hiển thị trang xác nhận xóa
        public ActionResult Delete(int id)
        {
            var model = _unitOfWork.Repository<DanhMuc>().GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }
            return View(model);
        }

        // 2. POST: Thực hiện xóa khi người dùng nhấn nút "Xóa"
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            _unitOfWork.Repository<DanhMuc>().Delete(id);
            _unitOfWork.Save();
            return RedirectToAction("Index");
        }


        // --- HELPER: Hàm đệ quy lấy tất cả ID con cháu ---
        // (Copy hàm này từ SanPhamController sang đây)
        private List<int> GetChildCategoryIDs(List<DanhMuc> allCats, int parentId)
        {
            var childIDs = new List<int>();

            // Tìm các con trực tiếp
            var children = allCats.Where(x => x.DanhMucChaID == parentId).Select(x => x.ID).ToList();

            foreach (var childId in children)
            {
                childIDs.Add(childId);
                // Đệ quy: Tìm tiếp con của con
                childIDs.AddRange(GetChildCategoryIDs(allCats, childId));
            }

            return childIDs;
        }

        // --- HELPER ---
        private SelectList GetDanhMucSelectList(int? selectedValue = null, int? excludeId = null)
        {
            // QUAN TRỌNG: Khai báo rõ ràng là IEnumerable để tránh lỗi convert
            IEnumerable<DanhMuc> query = _unitOfWork.Repository<DanhMuc>().GetAll();

            // Lọc dữ liệu trong bộ nhớ (LINQ to Objects)
            if (excludeId.HasValue)
            {
                query = query.Where(x => x.ID != excludeId.Value);
            }

            // Tạo SelectList, selectedValue sẽ giúp dropdown tự chọn đúng item
            return new SelectList(query, "ID", "TenDanhMuc", selectedValue);
        }
    }
}