using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Configuration;
using System.Data.SqlClient;
using Dapper;

namespace DoAn_Ver2.Models.AI_Services
{
    public class AIDataService
    {
        // Đọc chuỗi kết nối từ file Web.config (ví dụ tên chuỗi là "DefaultConnection")
        private string _connectionString = ConfigurationManager.ConnectionStrings["DoAn_DbModel"].ConnectionString;

        // Hàm này trả về list các đoạn text để lát nữa bạn mang đi tạo Vector
        public List<string> ExtractDataForAI()
        {
            // Do mình đã tạo View ở SQL rồi, nên câu lệnh C# giờ cực kỳ ngắn gọn!
            string query = "SELECT * FROM v_AI_SanPham";

            using (var connection = new SqlConnection(_connectionString))
            {
                // 1. Dapper tự động map dữ liệu từ View vào Model
                var products = connection.Query<SanPhamAIModel>(query).ToList();

                // 2. Map thành danh sách các câu văn cho AI
                List<string> documentsForAI = products
                    .Select(p => p.ToAITextDocument())
                    .ToList();

                return documentsForAI;
            }
        }
    }
}