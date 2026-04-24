using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models;
using PagedList;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;


namespace DoAn_Ver2.Areas.Admin.Controllers
{
    public class PaymentController : BaseController
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();
        // GET: Admin/Payment
        public ActionResult Index(string searchString, int? page)
        {
            var query = _unitOfWork.Repository<PhuongThucThanhToan>().GetAll();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(x => x.TenPhuongThuc.Contains(searchString) || x.MaCode.Contains(searchString));
            }

            query = query.OrderBy(x => x.ID);

            int pageSize = 10;
            int pageNumber = (page ?? 1);
            ViewBag.CurrentFilter = searchString;

            return View(query.ToPagedList(pageNumber, pageSize));
        }

        // 2. TẠO MỚI (GET)
        public ActionResult Create()
        {
            return View();
        }

        // 2. TẠO MỚI (POST) 
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)] 
        public ActionResult Create(PhuongThucThanhToan model, HttpPostedFileBase uploadBtn)
        {
            if (ModelState.IsValid)
            {
                var exist = _unitOfWork.Repository<PhuongThucThanhToan>().GetMany(x => x.MaCode == model.MaCode).FirstOrDefault();
                if (exist != null)
                {
                    ModelState.AddModelError("MaCode", "Mã Code này đã tồn tại (Ví dụ: COD, VNPAY...)");
                    return View(model);
                }

                if (uploadBtn != null && uploadBtn.ContentLength > 0)
                {
                    string _FileName = Path.GetFileName(uploadBtn.FileName);
                    string _path = Path.Combine(Server.MapPath("~/Content/images/payment/"), _FileName);

                    if (!Directory.Exists(Server.MapPath("~/Content/images/payment/")))
                    {
                        Directory.CreateDirectory(Server.MapPath("~/Content/images/payment/"));
                    }

                    uploadBtn.SaveAs(_path);
                    model.HinhAnh = "/Content/images/payment/" + _FileName;
                }

                model.MaCode = model.MaCode.ToUpper().Trim(); 

                _unitOfWork.Repository<PhuongThucThanhToan>().Add(model);
                _unitOfWork.Save();
                TempData["Success"] = "Thêm phương thức thanh toán thành công!";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // 3. CẬP NHẬT (GET)
        public ActionResult Edit(int id)
        {
            var item = _unitOfWork.Repository<PhuongThucThanhToan>().GetById(id);
            if (item == null) return HttpNotFound();
            return View(item);
        }

        // 3. CẬP NHẬT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Edit(PhuongThucThanhToan model, HttpPostedFileBase uploadBtn)
        {
            if (ModelState.IsValid)
            {
                var item = _unitOfWork.Repository<PhuongThucThanhToan>().GetById(model.ID);
                if (item == null) return HttpNotFound();
                item.TenPhuongThuc = model.TenPhuongThuc;
                item.MoTa = model.MoTa;
                item.TrangThai = model.TrangThai;
                item.MaCode = model.MaCode.ToUpper().Trim();
                if (uploadBtn != null && uploadBtn.ContentLength > 0)
                {
                    string _FileName = Path.GetFileName(uploadBtn.FileName);
                    string folderPath = Server.MapPath("~/Content/images/payment/");
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    string _path = Path.Combine(folderPath, _FileName);
                    uploadBtn.SaveAs(_path);

                    item.HinhAnh = "/Content/images/payment/" + _FileName;
                }

                _unitOfWork.Repository<PhuongThucThanhToan>().Update(item);
                _unitOfWork.Save();
                TempData["Success"] = "Cập nhật thành công!";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // 4. XÓA (POST)
        [HttpPost]
        public JsonResult Delete(int id)
        {
            try
            {
                var item = _unitOfWork.Repository<PhuongThucThanhToan>().GetById(id);
                if (item == null) return Json(new { success = false, message = "Không tìm thấy dữ liệu" });
                var checkOrder = _unitOfWork.Repository<DonHang>().GetMany(x => x.PhuongThucThanhToanID == id).FirstOrDefault();
                if (checkOrder != null)
                {
                    return Json(new { success = false, message = "Không thể xóa vì đã có đơn hàng sử dụng phương thức này. Hãy tắt trạng thái thay vì xóa." });
                }

                _unitOfWork.Repository<PhuongThucThanhToan>().Delete(item);
                _unitOfWork.Save();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}