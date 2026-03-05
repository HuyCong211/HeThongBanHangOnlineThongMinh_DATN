using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using DoAn_Ver2.Models;
using DoAn_Ver2.Infrastructure;

namespace DoAn_Ver2.Services
{
    public class RecommendationService
    {
        // Địa chỉ của API Python (Đảm bảo đang chạy ở port 5000)
        private readonly string _pythonApiUrl = "http://127.0.0.1:5000";
        // ---------------------------------------------------------
        // 1. TÍNH TOÁN GỢI Ý CHO NGƯỜI DÙNG (HIỂN THỊ TRANG CHỦ)
        // ---------------------------------------------------------
        /// <summary>
        /// Hàm tính toán Gợi ý cho riêng người dùng (User-Based) - Đã hỗ trợ cả Khách vãng lai
        /// </summary>
        public void CalculateUserRecommendations(int? userId, string sessionId)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // 1. Gắn tham số tùy thuộc vào việc khách đã đăng nhập hay chưa
                    string url = $"{_pythonApiUrl}/recommend_user?";
                    if (userId.HasValue)
                    {
                        url += $"user_id={userId.Value}";
                    }
                    else
                    {
                        url += $"session_id={sessionId}";
                    }

                    // 2. Gọi sang API Python
                    var response = client.GetAsync(url).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = response.Content.ReadAsStringAsync().Result;
                        dynamic data = JsonConvert.DeserializeObject(jsonResponse);

                        if (data.success == true)
                        {
                            using (var _unitOfWork = new UnitOfWork())
                            {
                                var repo = _unitOfWork.Repository<GoiYSanPham>();

                                // 3. XÓA dữ liệu "ForYou" cũ của người dùng / session này
                                var oldRecs = repo.GetMany(x =>
                                    (userId.HasValue ? x.NguoiDungID == userId.Value : x.SessionID == sessionId)
                                    && x.LoaiGoiY == "ForYou"
                                ).ToList();

                                foreach (var item in oldRecs)
                                {
                                    repo.Delete(item);
                                }

                                // 4. THÊM dữ liệu mới nhất
                                foreach (var rec in data.recommendations)
                                {
                                    var newRec = new GoiYSanPham
                                    {
                                        NguoiDungID = userId,
                                        SessionID = sessionId,
                                        SanPhamDuocGoiYID = (int)rec.SanPhamDuocGoiYID,
                                        DiemGoiY = (double)rec.DiemGoiY,
                                        LoaiGoiY = "ForYou", // Đánh dấu đây là gợi ý Trang chủ
                                        ThoiDiemTinhToan = DateTime.Now
                                    };
                                    repo.Add(newRec);
                                }
                                _unitOfWork.Save();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LỖI AI USER RECOMMEND: " + ex.Message);
            }
        }

        // ---------------------------------------------------------
        // 2. TÍNH TOÁN GỢI Ý CHO SẢN PHẨM (HIỂN THỊ TRANG CHI TIẾT)
        // ---------------------------------------------------------
        /// <summary>
        /// Hàm gọi AI Python để tính toán Sản phẩm tương tự (Content-Based)
        /// </summary>
        public void CalculateProductRecommendations(int productId)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // ==========================================
                    // 1. GỌI AI: SẢN PHẨM TƯƠNG TỰ (SIMILAR)
                    // ==========================================
                    string urlSimilar = $"{_pythonApiUrl}/recommend_similar?id={productId}";
                    var responseSimilar = client.GetAsync(urlSimilar).Result;

                    // ==========================================
                    // 2. GỌI AI: THƯỜNG MUA CÙNG (CO-BUY)
                    // ==========================================
                    string urlCoBuy = $"{_pythonApiUrl}/recommend_cobuy?id={productId}";
                    var responseCoBuy = client.GetAsync(urlCoBuy).Result;

                    using (var _unitOfWork = new UnitOfWork())
                    {
                        var repo = _unitOfWork.Repository<GoiYSanPham>();

                        // --- XỬ LÝ LƯU KẾT QUẢ "SIMILAR" ---
                        if (responseSimilar.IsSuccessStatusCode)
                        {
                            string jsonSimilar = responseSimilar.Content.ReadAsStringAsync().Result;
                            dynamic dataSimilar = JsonConvert.DeserializeObject(jsonSimilar);

                            if (dataSimilar.success == true)
                            {
                                var oldSimilar = repo.GetMany(x => x.SanPhamNguonID == productId && x.LoaiGoiY == "Similar").ToList();
                                foreach (var item in oldSimilar) repo.Delete(item);

                                foreach (var rec in dataSimilar.recommendations)
                                {
                                    repo.Add(new GoiYSanPham
                                    {
                                        SanPhamNguonID = productId,
                                        SanPhamDuocGoiYID = (int)rec.SanPhamDuocGoiYID,
                                        DiemGoiY = (double)rec.DiemGoiY,
                                        LoaiGoiY = "Similar",
                                        ThoiDiemTinhToan = DateTime.Now
                                    });
                                }
                            }
                        }

                        // --- XỬ LÝ LƯU KẾT QUẢ "CO-BUY" ---
                        if (responseCoBuy.IsSuccessStatusCode)
                        {
                            string jsonCoBuy = responseCoBuy.Content.ReadAsStringAsync().Result;
                            dynamic dataCoBuy = JsonConvert.DeserializeObject(jsonCoBuy);

                            if (dataCoBuy.success == true)
                            {
                                var oldCoBuy = repo.GetMany(x => x.SanPhamNguonID == productId && x.LoaiGoiY == "CoBuy").ToList();
                                foreach (var item in oldCoBuy) repo.Delete(item);

                                foreach (var rec in dataCoBuy.recommendations)
                                {
                                    repo.Add(new GoiYSanPham
                                    {
                                        SanPhamNguonID = productId,
                                        SanPhamDuocGoiYID = (int)rec.SanPhamDuocGoiYID,
                                        DiemGoiY = (double)rec.DiemGoiY,
                                        LoaiGoiY = "CoBuy",
                                        ThoiDiemTinhToan = DateTime.Now
                                    });
                                }
                            }
                        }

                        // Lưu cả 2 thay đổi xuống Database 1 lần
                        _unitOfWork.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LỖI AI RECOMMENDATION: " + ex.Message);
            }
        }


        // Helper
        private void AddScore(Dictionary<int, double> dict, int key, double val)
        {
            if (dict.ContainsKey(key)) dict[key] += val; else dict[key] = val;
        }
    }
}