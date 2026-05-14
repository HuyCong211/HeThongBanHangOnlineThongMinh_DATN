using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DoAn_Ver2.Models;
using DoAn_Ver2.Infrastructure;
using DoAn_Ver2.Models.ViewModel;
using System.Data.Entity;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks; 
using Newtonsoft.Json; 


namespace DoAn_Ver2.Controllers
{
    public class ProductController : Controller
    {
        private UnitOfWork _unitOfWork = new UnitOfWork();
        public ActionResult ProductByCategory(int? id, int? page, int? pageSize,
                                      string sort, string minPrice, string maxPrice,
                                      List<int> colorIds, List<int> sizeIds, string style)
        {
            int pNumber = page ?? 1;
            int pSize = pageSize ?? 12; 


            if (id == null) return RedirectToAction("Index", "Home");
            var danhMuc = _unitOfWork.Repository<DanhMuc>().GetById(id.Value); 
            if (danhMuc == null || danhMuc.TrangThai != 1) return HttpNotFound();
            ViewBag.CurrentCategory = danhMuc;
            var listId = new List<int> { id.Value };
            var subIds = _unitOfWork.Repository<DanhMuc>()
                            .GetMany(x => x.DanhMucChaID == id.Value && x.TrangThai == 1)
                            .Select(x => x.ID).ToList();
            listId.AddRange(subIds);
            var query = _unitOfWork.Repository<SanPham>()
                    .GetMany(x => x.DanhMucID != null && listId.Contains(x.DanhMucID.Value) && x.TrangThai == 1);

            // --- A. LỌC THEO GIÁ (Slider gửi lên string "100000" hoặc null) ---
            decimal? min = null, max = null;
            if (!string.IsNullOrEmpty(minPrice))
            {
                min = decimal.Parse(minPrice);
                query = query.Where(x => x.GiaBan >= min);
            }
            if (!string.IsNullOrEmpty(maxPrice))
            {
                max = decimal.Parse(maxPrice);
                query = query.Where(x => x.GiaBan <= max);
            }

            // --- B. LỌC THEO KIỂU DÁNG ---
            if (!string.IsNullOrEmpty(style))
            {
                query = query.Where(x => x.KieuDang == style);
            }

            // --- C. LỌC THEO MÀU (Sửa lại logic một chút) ---
            if (colorIds != null && colorIds.Any())
            {
                // Lấy SP có chứa ÍT NHẤT 1 trong các màu đã chọn
                var spHasColor = _unitOfWork.Repository<BienTheSanPham>()
                                    .GetMany(x => colorIds.Contains(x.MauSacID))
                                    .Select(x => x.SanPhamID).Distinct().ToList();
                query = query.Where(x => spHasColor.Contains(x.ID));
            }

            // --- LỌC THEO SIZE ---
            if (sizeIds != null && sizeIds.Any())
            {
                var spHasSize = _unitOfWork.Repository<BienTheSanPham>()
                                    .GetMany(x => sizeIds.Contains(x.KichThuocID))
                                    .Select(x => x.SanPhamID).Distinct().ToList();
                query = query.Where(x => spHasSize.Contains(x.ID));
            }

            // --- D. SẮP XẾP ---
            switch (sort)
            {
                case "price_asc": query = query.OrderBy(x => x.GiaBan); break;
                case "price_desc": query = query.OrderByDescending(x => x.GiaBan); break;
                case "name": query = query.OrderBy(x => x.TenSanPham); break;
                default: query = query.OrderByDescending(x => x.NgayTao); break; 
            }

            // --- E. PHÂN TRANG  ---
            int totalItems = query.Count();
            int totalPages = (int)Math.Ceiling((double)totalItems / pSize);

            var data = query.Skip((pNumber - 1) * pSize).Take(pSize).ToList();

            // 4. CHUẨN BỊ DỮ LIỆU SIDEBAR (Lấy tất cả màu/size có trong DB để hiện ra lọc)
            var allColors = _unitOfWork.Repository<MauSac>().GetAll().ToList();
            var allSizes = _unitOfWork.Repository<KichThuoc>().GetAll().ToList();
            var allStyles = _unitOfWork.Repository<SanPham>().GetAll()
                                .Where(x => x.KieuDang != null)
                                .Select(x => x.KieuDang).Distinct().ToList();

            // 5. ĐÓNG GÓI VIEWMODEL
            var model = new ProductListViewModel
            {
                Products = data,
                Colors = allColors,
                Sizes = allSizes,
                Styles = allStyles,
                CurrentCateID = id,
                SortBy = sort,
                PageSize = pSize,
                PageNumber = pNumber,
                TotalPages = totalPages,
                TotalItems = totalItems,
                MinPrice = min,
                MaxPrice = max,
                SelectedColors = colorIds ?? new List<int>(), 
                SelectedSizes = sizeIds ?? new List<int>(),
                SelectedStyle = style
            };

            return View(model);
        }

        // 2. TÌM KIẾM SẢN PHẨM
        public ActionResult Search(string keyword, int? page, int? pageSize,
                              string sort, string minPrice, string maxPrice,
                              List<int> colorIds, List<int> sizeIds, string style)
        {
            int pSize = 12;
            int pNumber = page ?? 1;
            ViewBag.Keyword = keyword;

            var activeCateIds = _unitOfWork.Repository<DanhMuc>()
                        .GetMany(x => x.TrangThai == 1)
                        .Select(x => x.ID).ToList();

            var activeCateNames = _unitOfWork.Repository<DanhMuc>()
                                    .GetMany(x => x.TrangThai == 1)
                                    .Select(x => x.TenDanhMuc).ToList();

            var query = _unitOfWork.Repository<SanPham>()
                .GetMany(x => (x.TenSanPham.Contains(keyword) || x.MoTa.Contains(keyword) || activeCateNames.Any(n => n.Contains(keyword)))
                              && x.TrangThai == 1
                              && x.DanhMucID != null && activeCateIds.Contains(x.DanhMucID.Value));

            // --- A. BỔ SUNG LỌC CHO TÌM KIẾM (Để khách search xong vẫn lọc giá/màu được) ---
            decimal? min = null, max = null;
            if (!string.IsNullOrEmpty(minPrice)) { min = decimal.Parse(minPrice); query = query.Where(x => x.GiaBan >= min); }
            if (!string.IsNullOrEmpty(maxPrice)) { max = decimal.Parse(maxPrice); query = query.Where(x => x.GiaBan <= max); }
            if (!string.IsNullOrEmpty(style)) { query = query.Where(x => x.KieuDang == style); }
            if (colorIds != null && colorIds.Any())
            {
                var spHasColor = _unitOfWork.Repository<BienTheSanPham>().GetMany(x => colorIds.Contains(x.MauSacID)).Select(x => x.SanPhamID).Distinct().ToList();
                query = query.Where(x => spHasColor.Contains(x.ID));
            }
            if (sizeIds != null && sizeIds.Any())
            {
                var spHasSize = _unitOfWork.Repository<BienTheSanPham>().GetMany(x => sizeIds.Contains(x.KichThuocID)).Select(x => x.SanPhamID).Distinct().ToList();
                query = query.Where(x => spHasSize.Contains(x.ID));
            }

            // --- B. SẮP XẾP ---
            switch (sort)
            {
                case "price_asc": query = query.OrderBy(x => x.GiaBan); break;
                case "price_desc": query = query.OrderByDescending(x => x.GiaBan); break;
                case "name": query = query.OrderBy(x => x.TenSanPham); break;
                default: query = query.OrderByDescending(x => x.NgayTao); break;
            }

            // --- C. PHÂN TRANG ---
            int totalItems = query.Count();
            int totalPages = (int)Math.Ceiling((double)totalItems / pSize);
            var data = query.Skip((pNumber - 1) * pSize).Take(pSize).ToList();

            var model = new ProductListViewModel
            {
                Products = data,
                Colors = _unitOfWork.Repository<MauSac>().GetAll().ToList(),
                Sizes = _unitOfWork.Repository<KichThuoc>().GetAll().ToList(),
                Styles = _unitOfWork.Repository<SanPham>().GetAll().Where(x => x.KieuDang != null).Select(x => x.KieuDang).Distinct().ToList(),
                CurrentCateID = null, 
                PageNumber = pNumber,
                PageSize = pSize,
                TotalPages = totalPages,
                TotalItems = totalItems,
                SortBy = sort,
                MinPrice = min,
                MaxPrice = max,
                SelectedColors = colorIds ?? new List<int>(),
                SelectedSizes = sizeIds ?? new List<int>(),
                SelectedStyle = style
            };

            // --- [AI TRACKING] GHI NHẬN LỊCH SỬ TÌM KIẾM ---
            string sessionId = GetOrSetGuestSessionId();
            int? userId = (Session["KhachHang"] != null) ? ((NguoiDung)Session["KhachHang"]).ID : (int?)null;

            var repoSearch = _unitOfWork.Repository<LichSuTimKiem>();
            var lastSearch = repoSearch.GetMany(x => (userId != null ? x.NguoiDungID == userId : x.SessionID == sessionId))
                                       .OrderByDescending(x => x.ThoiGian).FirstOrDefault();

            if (lastSearch == null || lastSearch.TuKhoa.ToLower() != keyword.ToLower())
            {
                repoSearch.Add(new LichSuTimKiem { NguoiDungID = userId, SessionID = sessionId, TuKhoa = keyword, ThoiGian = DateTime.Now });
                _unitOfWork.Save();
            }
            return View("ProductByCategory", model);
        }






        // ---------------------------------------------------------
        // 2. ACTION TÌM KIẾM BẰNG ẢNH (GỌI SANG AI SERVER)
        // ---------------------------------------------------------
        [HttpPost]
        public async Task<ActionResult> SearchByImage(HttpPostedFileBase imageFile)
        {
            if (imageFile != null && imageFile.ContentLength > 0)
            {
                try
                {
                    // --- 1. XỬ LÝ HIỂN THỊ ẢNH (MỚI THÊM) ---
                    // Chuyển file ảnh sang dạng Base64 để gửi xuống View hiển thị luôn
                    string base64Image = "";
                    // Đọc trực tiếp vào mảng byte thay vì dùng 'using BinaryReader'
                    byte[] fileData = new byte[imageFile.ContentLength];
                    imageFile.InputStream.Read(fileData, 0, imageFile.ContentLength);

                    base64Image = "data:image/png;base64," + Convert.ToBase64String(fileData);
                    ViewBag.SearchImage = base64Image;

                    ViewBag.SearchImage = base64Image;

                    // Reset lại vị trí đọc file về 0
                    imageFile.InputStream.Position = 0;


                    // --- 2. GỬI SANG PYTHON ---
                    List<SanPham> foundProducts = new List<SanPham>();
                    HashSet<int> addedProductIds = new HashSet<int>();

                    using (var client = new HttpClient())
                    {
                        var content = new MultipartFormDataContent();
                        content.Add(new StreamContent(imageFile.InputStream), "file", imageFile.FileName);

                        var response = await client.PostAsync("http://127.0.0.1:5000/predict", content);
                        var responseString = await response.Content.ReadAsStringAsync();
                        dynamic result = JsonConvert.DeserializeObject(responseString);

                        if (result.success == true)
                        {
                            foreach (var item in result.matches)
                            {
                                string imageName = item.image_name;
                                string fileNameOnly = Path.GetFileName(imageName);

                                var anhSP = _unitOfWork.Repository<AnhSanPham>()
                                    .GetMany(x => x.URL.Contains(fileNameOnly))
                                    .FirstOrDefault();

                                if (anhSP != null)
                                {
                                    var product = _unitOfWork.Repository<SanPham>().GetById(anhSP.SanPhamID);
                                    if (product != null && product.TrangThai == 1 && !addedProductIds.Contains(product.ID))
                                    {
                                        foundProducts.Add(product);
                                        addedProductIds.Add(product.ID);
                                    }
                                }
                            }
                        }
                    }

                    // --- 3. TRẢ VỀ VIEW ---
                    var model = new ProductListViewModel
                    {
                        Products = foundProducts,
                        Colors = _unitOfWork.Repository<MauSac>().GetAll().ToList(),
                        Sizes = _unitOfWork.Repository<KichThuoc>().GetAll().ToList(),
                        PageNumber = 1,
                        PageSize = foundProducts.Count > 0 ? foundProducts.Count : 1,
                        TotalPages = 1,
                        SortBy = "relevance"
                    };

                    ViewBag.Keyword = foundProducts.Count > 0
                        ? $"Tìm thấy {foundProducts.Count} sản phẩm tương tự."
                        : "Không tìm thấy sản phẩm nào giống ảnh này.";

                    return View("ProductByCategory", model);
                }
                catch (Exception ex)
                {
                    return Content("Lỗi: " + ex.Message);
                }
            }
            return RedirectToAction("Index", "Product");
        }







        //====================================
        // 3. CHI TIẾT SẢN PHẨM 
        //====================================
        public ActionResult Detail(int id)
        {
            var sp = _unitOfWork.Repository<SanPham>().GetById(id);
            if (sp == null || sp.TrangThai != 1) return HttpNotFound();

            // Load DanhMuc riêng để tránh lỗi lazy loading null
            DanhMuc spDanhMuc = null;
            if (sp.DanhMucID.HasValue)
                spDanhMuc = _unitOfWork.Repository<DanhMuc>().GetById(sp.DanhMucID.Value);
            if (spDanhMuc == null || spDanhMuc.TrangThai != 1) return HttpNotFound();
            sp.LuotXem++;
            _unitOfWork.Repository<SanPham>().Update(sp);
            _unitOfWork.Save();

            AddToHistory(id);

            var listSKU = _unitOfWork.Repository<BienTheSanPham>()
                            .GetMany(x => x.SanPhamID == id).ToList();
            var listAnh = _unitOfWork.Repository<AnhSanPham>()
                            .GetMany(x => x.SanPhamID == id).ToList();
            var listMauID = listSKU.Select(x => x.MauSacID).Distinct().ToList();
            var listMau = _unitOfWork.Repository<MauSac>()
                            .GetMany(x => listMauID.Contains(x.ID)).ToList();
            var listSizeID = listSKU.Select(x => x.KichThuocID).Distinct().ToList();
            var listSize = _unitOfWork.Repository<KichThuoc>()
                            .GetMany(x => listSizeID.Contains(x.ID)).ToList();
            var reviews = _unitOfWork.Repository<DanhGia>()
                .GetMany(x => x.SanPhamID == id && (x.TrangThai == true || x.TrangThai == null))
                .OrderByDescending(x => x.NgayDanhGia).ToList();
            double avgStar = reviews.Any() ? reviews.Average(x => x.SoSao ?? 0) : 0;
            int reviewCount = reviews.Count;

            var reviewUserIds = reviews.Select(x => x.NguoiDungID).Distinct().ToList();
            var reviewUsers = _unitOfWork.Repository<NguoiDung>()
                .GetMany(x => reviewUserIds.Contains(x.ID))
                .ToDictionary(x => x.ID, x => x.TenDangNhap);
            ViewBag.ReviewUsers = reviewUsers;

            ViewBag.Reviews = reviews;
            ViewBag.AvgStar = avgStar;
            ViewBag.ReviewCount = reviewCount;

            // ---LOGIC KIỂM TRA QUYỀN ĐÁNH GIÁ TỐI ƯU ---
            bool canReview = false;
            int remainingReviews = 0;
            if (Session["KhachHang"] != null)
            {
                var user = (NguoiDung)Session["KhachHang"];

                var completedOrderIds = _unitOfWork.Repository<DonHang>()
                    .GetMany(d => d.NguoiDungID == user.ID && d.TrangThaiDonHang == 3)
                    .Select(d => d.ID).ToList();

                if (completedOrderIds.Any())
                {
                    var currentSkuIds = listSKU.Select(s => s.ID).ToList();
                    int totalBought = _unitOfWork.Repository<ChiTietDonHang>()
                        .GetMany(ct => completedOrderIds.Contains(ct.DonHangID) && currentSkuIds.Contains(ct.BienTheSanPhamID))
                        .Select(ct => ct.DonHangID).Distinct().Count();
                    int totalReviewed = _unitOfWork.Repository<DanhGia>()
                        .GetMany(x => x.SanPhamID == id && x.NguoiDungID == user.ID).Count();

                    remainingReviews = totalBought - totalReviewed;
                    canReview = remainingReviews > 0;
                }
            }
            ViewBag.CanReview = canReview;
            ViewBag.RemainingReviews = remainingReviews;


            // =========================================================
            // LẤY SỐ LƯỢNG ĐÃ CÓ TRONG GIỎ HÀNG ĐỂ TRỪ ĐI
            // =========================================================
            var cartQtyDict = new Dictionary<int, int>();
            if (Session["KhachHang"] != null)
            {
                var user = (NguoiDung)Session["KhachHang"];
                var cart = _unitOfWork.Repository<GioHang>().GetMany(x => x.NguoiDungID == user.ID).FirstOrDefault();
                if (cart != null)
                {
                    var cartItems = _unitOfWork.Repository<GioHangChiTiet>().GetMany(x => x.GioHangID == cart.ID).ToList();
                    foreach (var item in cartItems)
                    {
                        cartQtyDict[item.BienTheSanPhamID] = item.SoLuong ?? 0;
                    }
                }
            }
            else
            {
                var sessionCart = Session["Cart"] as List<CartItemViewModel>;
                if (sessionCart != null)
                {
                    foreach (var item in sessionCart)
                    {
                        cartQtyDict[item.SKU_ID] = item.SoLuong;
                    }
                }
            }
            ViewBag.CartQtyDict = cartQtyDict; 
            // =========================================================






            // =========================================================
            // [PHẦN AI]
            // =========================================================

            // A. "Sản phẩm tương tự" (Similar)
            var similarProducts = new List<SanPham>();

            // B1. Thử lấy từ AI
            var similarIds = _unitOfWork.Repository<GoiYSanPham>()
                .GetMany(x => x.SanPhamNguonID == id && x.LoaiGoiY == "Similar")
                .OrderByDescending(x => x.DiemGoiY)
                .Select(x => x.SanPhamDuocGoiYID)
                .Take(8).ToList();

            if (similarIds.Any())
            {
                var raw = _unitOfWork.Repository<SanPham>().GetMany(x => similarIds.Contains(x.ID) && x.TrangThai == 1).ToList();
                similarProducts = similarIds.Join(raw, sID => sID, p => p.ID, (sID, p) => p).ToList();
            }

            // B2. Nếu AI rỗng -> Dùng logic Fallback ngay tại đây (Cùng danh mục)
            if (similarProducts.Count == 0 && sp.DanhMucID.HasValue)
            {
                similarProducts = _unitOfWork.Repository<SanPham>()
                    .GetMany(x => x.DanhMucID == sp.DanhMucID && x.ID != id && x.TrangThai == 1)
                    .OrderByDescending(x => x.LuotXem).Take(8).ToList();
            }

            // B. "Thường mua cùng" 
            var boughtTogetherProducts = new List<SanPham>();
            var boughtIds = _unitOfWork.Repository<GoiYSanPham>()
                .GetMany(x => x.SanPhamNguonID == id && x.LoaiGoiY == "CoBuy")
                .OrderByDescending(x => x.DiemGoiY)
                .Select(x => x.SanPhamDuocGoiYID)
                .Take(6).ToList();

            if (boughtIds.Any())
            {
                var raw = _unitOfWork.Repository<SanPham>().GetMany(x => boughtIds.Contains(x.ID) && x.TrangThai == 1).ToList();
                boughtTogetherProducts = boughtIds.Join(raw, sID => sID, p => p.ID, (sID, p) => p).ToList();
            }
            ViewBag.ListSKU = listSKU;
            ViewBag.ListAnh = listAnh;
            ViewBag.ListMau = listMau;
            ViewBag.ListSize = listSize;

            //ViewBag chứa dữ liệu AI
            ViewBag.SimilarProducts = similarProducts;
            ViewBag.BoughtTogetherProducts = boughtTogetherProducts;



            // =========================================================
            //  6. KÍCH HOẠT AI CHẠY NGẦM (TRIGGER)
            // =========================================================
            // =========================================================
            // [TRIGGER AI THÔNG MINH]
            // =========================================================
            var aiService = new DoAn_Ver2.Services.RecommendationService();
            if (similarProducts.Count == 0 && boughtTogetherProducts.Count == 0)
            {
                aiService.CalculateProductRecommendations(id);

                // 1. Lấy lại Similar
                var newSimilarIds = _unitOfWork.Repository<GoiYSanPham>()
                    .GetMany(x => x.SanPhamNguonID == id && x.LoaiGoiY == "Similar")
                    .OrderByDescending(x => x.DiemGoiY).Select(x => x.SanPhamDuocGoiYID).Take(8).ToList();
                if (newSimilarIds.Any())
                {
                    var raw = _unitOfWork.Repository<SanPham>().GetMany(x => newSimilarIds.Contains(x.ID) && x.TrangThai == 1).ToList();
                    ViewBag.SimilarProducts = newSimilarIds.Join(raw, sID => sID, p => p.ID, (sID, p) => p).ToList();
                }

                // 2. Lấy lại CoBuy
                var newBoughtIds = _unitOfWork.Repository<GoiYSanPham>()
                    .GetMany(x => x.SanPhamNguonID == id && x.LoaiGoiY == "CoBuy")
                    .OrderByDescending(x => x.DiemGoiY).Select(x => x.SanPhamDuocGoiYID).Take(6).ToList();
                if (newBoughtIds.Any())
                {
                    var raw = _unitOfWork.Repository<SanPham>().GetMany(x => newBoughtIds.Contains(x.ID) && x.TrangThai == 1).ToList();
                    ViewBag.BoughtTogetherProducts = newBoughtIds.Join(raw, sID => sID, p => p.ID, (sID, p) => p).ToList();
                }
            }
            else
            {
                Task.Run(() => aiService.CalculateProductRecommendations(id));
            }
            if (Session["KhachHang"] != null)
            {
                var user = (NguoiDung)Session["KhachHang"];
                string currentSessionId = GetOrSetGuestSessionId();
                Task.Run(() => new DoAn_Ver2.Services.RecommendationService().CalculateUserRecommendations(user.ID, currentSessionId));
            }

            return View(sp);
        }



        [ChildActionOnly]
        public ActionResult _SidebarCategoryPartial(int? categoryId)
        {
            if (categoryId == null)
            {
                var rootCats = _unitOfWork.Repository<DanhMuc>().GetMany(x => x.DanhMucChaID == null).ToList();
                ViewBag.TitleBlock = "DANH MỤC";
                return PartialView(rootCats);
            }

            var current = _unitOfWork.Repository<DanhMuc>().GetById(categoryId.Value);

            int rootId;
            string rootName;

            if (current.DanhMucChaID == null)
            {
                rootId = current.ID;
                rootName = current.TenDanhMuc;
            }
            else
            {
                rootId = current.DanhMucChaID.Value;
                var parent = _unitOfWork.Repository<DanhMuc>().GetById(rootId);
                rootName = parent.TenDanhMuc;
            }

            ViewBag.TitleBlock = rootName; 

            var subCats = _unitOfWork.Repository<DanhMuc>()
                            .GetMany(x => x.DanhMucChaID == rootId)
                            .ToList();

            return PartialView(subCats);
        }







        // Action Gửi đánh giá
        [HttpPost]
        public ActionResult SubmitReview(int SanPhamID, int SoSao, string BinhLuan)
        {
            if (Session["KhachHang"] == null) return RedirectToAction("Login", "Account");

            var user = (NguoiDung)Session["KhachHang"];

            // --- LOGIC CHẶN SPAM KÉP TẠI BACKEND ---
            var completedOrderIds = _unitOfWork.Repository<DonHang>()
                .GetMany(d => d.NguoiDungID == user.ID && d.TrangThaiDonHang == 3)
                .Select(d => d.ID).ToList();

            int totalBought = 0;
            if (completedOrderIds.Any())
            {
                var skuIds = _unitOfWork.Repository<BienTheSanPham>()
                    .GetMany(s => s.SanPhamID == SanPhamID).Select(s => s.ID).ToList();
                totalBought = _unitOfWork.Repository<ChiTietDonHang>()
                    .GetMany(ct => completedOrderIds.Contains(ct.DonHangID) && skuIds.Contains(ct.BienTheSanPhamID))
                    .Select(ct => ct.DonHangID).Distinct().Count();
            }

            int totalReviewed = _unitOfWork.Repository<DanhGia>()
                .GetMany(x => x.SanPhamID == SanPhamID && x.NguoiDungID == user.ID).Count();

            if (totalReviewed >= totalBought)
            {
                TempData["Error"] = "Bạn đã đánh giá cho sản phẩm này! Mua thêm để có đánh giá lượt mới.";
                return RedirectToAction("Detail", new { id = SanPhamID });
            }

            var review = new DanhGia
            {
                SanPhamID = SanPhamID,
                NguoiDungID = user.ID,
                SoSao = SoSao,
                BinhLuan = BinhLuan,
                NgayDanhGia = DateTime.Now
            };
            _unitOfWork.Repository<DanhGia>().Add(review);
            _unitOfWork.Save();

            TempData["Message"] = "Đánh giá của bạn đã được gửi thành công!";
            return RedirectToAction("Detail", new { id = SanPhamID });
        }


        // 2. HÀM XỬ LÝ LOGIC LƯU 
        private void AddToHistory(int productId)
        {
            string sessionId = GetOrSetGuestSessionId();
            int? userId = null;
            if (Session["KhachHang"] != null)
            {
                userId = ((NguoiDung)Session["KhachHang"]).ID;
            }

            var repo = _unitOfWork.Repository<LichSuXem>();

            var existItem = repo.GetMany(x =>
                (userId != null && x.NguoiDungID == userId && x.SanPhamID == productId) ||
                (userId == null && x.SessionID == sessionId && x.SanPhamID == productId)
            ).FirstOrDefault();

            if (existItem != null)
            {
                existItem.ThoiGian = DateTime.Now;
                repo.Update(existItem);
            }
            else
            {
                var newItem = new LichSuXem
                {
                    NguoiDungID = userId,
                    SessionID = sessionId, 
                    SanPhamID = productId,
                    ThoiGian = DateTime.Now
                };
                repo.Add(newItem);
            }
            _unitOfWork.Save();

            if (Session["KhachHang"] == null)
            {
                var cookie = Request.Cookies["RecentView"];
                string val = cookie != null ? cookie.Value : "";
                var ids = !string.IsNullOrEmpty(val) ? val.Split(',').Select(int.Parse).ToList() : new List<int>();
                if (ids.Contains(productId)) ids.Remove(productId);
                ids.Insert(0, productId);
                if (ids.Count > 10) ids = ids.Take(10).ToList();
                var newCookie = new HttpCookie("RecentView", string.Join(",", ids));
                newCookie.Expires = DateTime.Now.AddDays(30);
                Response.Cookies.Add(newCookie);
            }
        }

        // 3. ACTION HIỂN THỊ WIDGET
        [ChildActionOnly]
        public ActionResult _RecentlyViewedFloat()
        {
            List<int> ids = new List<int>();

            if (Session["KhachHang"] != null)
            {
                var user = (NguoiDung)Session["KhachHang"];
                ids = _unitOfWork.Repository<LichSuXem>()
                        .GetMany(x => x.NguoiDungID == user.ID)
                        .OrderByDescending(x => x.ThoiGian)
                        .Select(x => x.SanPhamID)
                        .Take(5)
                        .ToList();
            }
            else
            {
                var cookie = Request.Cookies["RecentView"];
                if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
                {
                    try { ids = cookie.Value.Split(',').Select(int.Parse).ToList().Take(5).ToList(); } catch { }
                }
            }

            var products = new List<SanPham>();
            if (ids.Any())
            {
                var raw = _unitOfWork.Repository<SanPham>()
                            .GetMany(x => ids.Contains(x.ID) && x.TrangThai == 1)
                            .ToList();

                products = ids.Select(id => raw.FirstOrDefault(p => p.ID == id))
                              .Where(p => p != null)
                              .ToList();
                foreach (var p in products)
                {
                    var img = _unitOfWork.Repository<AnhSanPham>()
                                .GetMany(x => x.SanPhamID == p.ID && x.MacDinh == true)
                                .FirstOrDefault();
                    ViewData["Img_" + p.ID] = img != null ? img.URL : "https://via.placeholder.com/100";
                }
            }

            return PartialView(products);
        }

        // 4. ACTION TRANG LỊCH SỬ XEM
        public ActionResult History(int? page)
        {
            int pageSize = 8; 
            int pageNumber = (page ?? 1); 
            List<int> allIds = new List<int>();

            if (Session["KhachHang"] != null)
            {
                var user = (NguoiDung)Session["KhachHang"];
                allIds = _unitOfWork.Repository<LichSuXem>()
                        .GetMany(x => x.NguoiDungID == user.ID)
                        .OrderByDescending(x => x.ThoiGian)
                        .Select(x => x.SanPhamID)
                        .Take(50)
                        .ToList();
            }
            else
            {
                var cookie = Request.Cookies["RecentView"];
                if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
                {
                    try
                    {
                        allIds = cookie.Value.Split(',').Select(int.Parse).ToList();
                    }
                    catch { }
                }
            }
            int totalItems = allIds.Count;
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            if (pageNumber > totalPages) pageNumber = totalPages;
            if (pageNumber < 1) pageNumber = 1;
            var pagedIds = allIds.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            // 3. LẤY CHI TIẾT SẢN PHẨM & ẢNH 
            var products = new List<SanPham>();
            var productImages = new Dictionary<int, string>();

            if (pagedIds.Any())
            {
                var rawProducts = _unitOfWork.Repository<SanPham>()
                                    .GetMany(x => pagedIds.Contains(x.ID) && x.TrangThai == 1)
                                    .ToList();
                products = pagedIds.Select(id => rawProducts.FirstOrDefault(p => p.ID == id))
                                   .Where(p => p != null)
                                   .ToList();
                var repoAnh = _unitOfWork.Repository<AnhSanPham>();
                foreach (var p in products)
                {
                    var img = repoAnh.GetMany(x => x.SanPhamID == p.ID && x.MacDinh == true).FirstOrDefault();
                    string url = img != null ? img.URL : (repoAnh.GetMany(x => x.SanPhamID == p.ID).FirstOrDefault()?.URL ?? "https://via.placeholder.com/300");
                    productImages[p.ID] = url;
                }
            }
            ViewBag.ProductImages = productImages;
            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(products);
        }


        // --- HÀM HỖ TRỢ AI TRACKING ---
        private string GetOrSetGuestSessionId()
        {
            string cookieName = "AI_Guest_SessionID";
            if (Request.Cookies[cookieName] != null)
            {
                return Request.Cookies[cookieName].Value;
            }
            else
            {
                string newSessionId = Guid.NewGuid().ToString();
                HttpCookie cookie = new HttpCookie(cookieName, newSessionId);
                cookie.Expires = DateTime.Now.AddDays(30); 
                Response.Cookies.Add(cookie);
                return newSessionId;
            }
        }


        protected override void Dispose(bool disposing)
        {
            _unitOfWork.Dispose();
            base.Dispose(disposing);
        }
    }
}