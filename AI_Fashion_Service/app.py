import os
import numpy as np
import pyodbc
import pandas as pd
from flask import Flask, request, jsonify
from tensorflow.keras.applications.mobilenet_v2 import MobileNetV2, preprocess_input
from tensorflow.keras.preprocessing import image
from PIL import Image
from sklearn.metrics.pairwise import cosine_similarity
from sklearn.feature_extraction.text import TfidfVectorizer

app = Flask(__name__)

# ==========================================
# PHẦN 1: TÌM KIẾM BẰNG HÌNH ẢNH (GIỮ NGUYÊN)
# ==========================================

# --- CẤU HÌNH ---
IMAGE_FOLDER = 'static/images'

# Load Model (Chỉ lấy phần trích xuất đặc trưng)
print("--- DANG KHOI DONG AI... ---")
model = MobileNetV2(weights='imagenet', include_top=False, pooling='avg')
print("--- MODEL DA SAN SANG! ---")

# Biến lưu trữ data trong RAM
features_db = []  # Chứa các dãy số (vectors)
image_names_db = []  # Chứa tên file tương ứng (vd: ao_so_mi.jpg)


# --- HÀM 1: Biến ảnh thành Vector ---
def extract_feature(img_path, from_stream=False):
    try:
        if from_stream:
            img = Image.open(img_path).convert('RGB')
        else:
            img = Image.open(img_path).convert('RGB')

        img = img.resize((224, 224))
        x = image.img_to_array(img)
        x = np.expand_dims(x, axis=0)
        x = preprocess_input(x)
        feature = model.predict(x)
        return feature.flatten()
    except Exception as e:
        return None


# --- HÀM 2: Học (Index) tất cả ảnh trong folder ---
def index_images():
    print(f"--- DANG QUET THU MUC {IMAGE_FOLDER}... ---")
    if not os.path.exists(IMAGE_FOLDER):
        print("Loi: Khong tim thay thu muc anh!")
        return

    # Lấy danh sách file
    files = [f for f in os.listdir(IMAGE_FOLDER) if f.lower().endswith(('.png', '.jpg', '.jpeg', '.webp'))]

    count = 0
    for img_name in files:
        img_path = os.path.join(IMAGE_FOLDER, img_name)
        feat = extract_feature(img_path, from_stream=False)

        if feat is not None:
            features_db.append(feat)
            image_names_db.append(img_name)
            count += 1
            # In ra cứ mỗi 50 ảnh để biết nó đang chạy
            if count % 50 == 0: print(f"Da hoc xong {count} anh...")

    print(f"--- HOAN TAT! DA HOC {len(image_names_db)} ANH. ---")


# Chạy hàm học ngay khi bật server
index_images()


# --- API TÌM KIẾM ---
@app.route('/predict', methods=['POST'])
def search():
    if 'file' not in request.files:
        return jsonify({'success': False, 'message': 'Chưa gửi file'})

    file = request.files['file']
    try:
        # 1. Xử lý ảnh người dùng gửi lên
        query_vector = extract_feature(file.stream, from_stream=True)
        if query_vector is None:
            return jsonify({'success': False, 'message': 'Ảnh lỗi không đọc được'})

        query_vector = query_vector.reshape(1, -1)

        if len(features_db) == 0:
            return jsonify({'success': False, 'message': 'Kho ảnh rỗng.'})

        # 2. Tính độ giống với TOÀN BỘ kho ảnh
        similarities = cosine_similarity(query_vector, features_db)

        # 3. Lấy Top 10 ảnh giống nhất (Sắp xếp giảm dần)
        # Tại sao lấy 10? Để lỡ 5 cái đầu là sản phẩm hết hàng thì vẫn còn 5 cái sau
        top_indices = np.argsort(similarities[0])[::-1][:10]

        results = []
        for i in top_indices:
            score = similarities[0][i]
            # Chỉ lấy nếu độ giống > 50% (0.5)
            if score > 0.8:
                results.append({
                    'image_name': image_names_db[i],  # Tên file gốc (quan trọng nhất)
                    'score': float(score)
                })

        return jsonify({
            'success': True,
            'matches': results  # Trả về danh sách
        })

    except Exception as e:
        print(e)
        return jsonify({'success': False, 'message': str(e)})

# ==========================================
# API ĐỒNG BỘ ẢNH MỚI (CHẠY NGẦM KHI ADMIN THÊM SP)
# ==========================================
@app.route('/add_index', methods=['POST'])
def add_index():
    if 'file' not in request.files:
        return jsonify({'success': False, 'message': 'Chưa gửi file'})

    file = request.files['file']
    image_name = request.form.get('image_name')  # Nhận tên file chính thức từ C#

    if not image_name:
        return jsonify({'success': False, 'message': 'Thiếu tên file (image_name)'})

    try:
        # 1. Lưu file vật lý vào ổ cứng để dự phòng khi Python server khởi động lại
        if not os.path.exists(IMAGE_FOLDER):
            os.makedirs(IMAGE_FOLDER)
        save_path = os.path.join(IMAGE_FOLDER, image_name)
        file.save(save_path)

        # 2. Trích xuất đặc trưng AI ngay lập tức
        feat = extract_feature(save_path, from_stream=False)

        if feat is not None:
            # 3. Đưa thẳng vào RAM (Dữ liệu Live)
            features_db.append(feat)
            image_names_db.append(image_name)
            return jsonify({'success': True, 'message': f'Đã đồng bộ {image_name} vào AI'})
        else:
            return jsonify({'success': False, 'message': 'Lỗi trích xuất vector AI'})

    except Exception as e:
        print("LỖI ĐỒNG BỘ AI:", str(e))
        return jsonify({'success': False, 'message': str(e)})



# ==========================================
# [THÊM MỚI] PHẦN 2: AI GỢI Ý SẢN PHẨM TƯƠNG TỰ (TEXT-BASED)
# ==========================================

# --- HÀM KẾT NỐI DATABASE SQL SERVER ---
def get_db_connection():
    # LƯU Ý QUAN TRỌNG: Hãy sửa 'localhost\\SQLEXPRESS' thành tên SQL Server của bạn
    conn_str = (
        r'DRIVER={ODBC Driver 17 for SQL Server};'
        r'SERVER=DESKTOP-PC8H4EK\SQLEXPRESS;'  # <-- SỬA CHỖ NÀY NẾU CẦN
        r'DATABASE=DoAnTotNghiep_Test;'
        r'Trusted_Connection=yes;'
    )
    return pyodbc.connect(conn_str)


# --- API GỢI Ý ---
@app.route('/recommend_similar', methods=['GET'])
def recommend_similar():
    try:
        # Lấy ID sản phẩm khách đang xem
        product_id = request.args.get('id')
        if not product_id:
            return jsonify({'success': False, 'message': 'Thiếu tham số id'})

        product_id = int(product_id)

        # 1. Truy vấn SQL lấy Tên, Kiểu dáng, Danh mục
        conn = get_db_connection()
        query = """
            SELECT s.ID, s.TenSanPham, s.KieuDang, d.TenDanhMuc 
            FROM SanPham s
            LEFT JOIN DanhMuc d ON s.DanhMucID = d.ID
            WHERE s.TrangThai = 1
        """
        df = pd.read_sql(query, conn)
        conn.close()

        if df.empty:
            return jsonify({'success': False, 'message': 'Không có dữ liệu sản phẩm'})

        if product_id not in df['ID'].values:
            return jsonify({'success': False, 'message': 'ID Sản phẩm không tồn tại hoặc đã bị ẩn'})

        # 2. Xử lý dữ liệu văn bản
        df['KieuDang'] = df['KieuDang'].fillna('')
        df['TenDanhMuc'] = df['TenDanhMuc'].fillna('')
        df['Features'] = df['TenSanPham'] + " " + df['KieuDang'] + " " + df['TenDanhMuc']

        # 3. Biến văn bản thành Vector (TF-IDF)
        vectorizer = TfidfVectorizer()
        tfidf_matrix = vectorizer.fit_transform(df['Features'])

        # 4. Tính Cosine Similarity
        cosine_sim = cosine_similarity(tfidf_matrix, tfidf_matrix)

        # 5. Lấy danh sách gợi ý
        idx = df.index[df['ID'] == product_id][0]
        sim_scores = list(enumerate(cosine_sim[idx]))

        # Sắp xếp giảm dần, bỏ qua chính nó ở vị trí số 0
        sim_scores = sorted(sim_scores, key=lambda x: x[1], reverse=True)
        top_similar = sim_scores[1:9]  # Lấy 8 sản phẩm liên quan nhất

        recommendations = []
        for i in top_similar:
            matched_id = int(df.iloc[i[0]]['ID'])
            score = float(i[1])
            if score > 0:  # Chỉ lấy SP có độ tương đồng > 0
                recommendations.append({
                    'SanPhamDuocGoiYID': matched_id,
                    'DiemGoiY': round(score, 4)
                })

        return jsonify({
            'success': True,
            'product_id': product_id,
            'recommendations': recommendations
        })

    except Exception as e:
        print("LỖI RECOMMEND:", str(e))
        return jsonify({'success': False, 'error': str(e)})


# ==========================================
# PHẦN 3: AI GỢI Ý CÁ NHÂN HÓA (ĐÃ NÂNG CẤP THEO BÀI TOÁN CỦA BẠN)
# ==========================================

@app.route('/recommend_user', methods=['GET'])
def recommend_user():
    try:
        user_id = request.args.get('user_id')
        session_id = request.args.get('session_id')

        conn = get_db_connection()
        seed_product_ids = set()  # Tập hợp "Hạt giống" sở thích (Dùng Set để không bị trùng)
        interacted_ids = set()  # Những sản phẩm khách ĐÃ xem/mua/thích (để lát nữa không gợi ý lại)

        # ---------------------------------------------------------
        # BƯỚC 1: THU THẬP DỮ LIỆU ĐỂ TẠO "HỒ SƠ SỞ THÍCH"
        # ---------------------------------------------------------
        if user_id and user_id.lower() != 'null' and user_id != '':
            # 1. Lấy SP đã xem
            df_xem = pd.read_sql(
                f"SELECT TOP 10 SanPhamID FROM LichSuXem WHERE NguoiDungID = {user_id} ORDER BY ThoiGian DESC", conn)
            seed_product_ids.update(df_xem['SanPhamID'].tolist())

            # 2. Lấy SP Yêu thích
            df_thich = pd.read_sql(
                f"SELECT TOP 10 SanPhamID FROM SanPhamYeuThich WHERE NguoiDungID = {user_id} ORDER BY NgayThem DESC",
                conn)
            seed_product_ids.update(df_thich['SanPhamID'].tolist())

            # 3. Lấy SP Đã mua (Qua ChiTietDonHang -> BienTheSanPham)
            query_mua = f"""
                SELECT TOP 10 b.SanPhamID 
                FROM ChiTietDonHang c
                JOIN DonHang d ON c.DonHangID = d.ID
                JOIN BienTheSanPham b ON c.BienTheSanPhamID = b.ID
                WHERE d.NguoiDungID = {user_id}
            """
            df_mua = pd.read_sql(query_mua, conn)
            seed_product_ids.update(df_mua['SanPhamID'].tolist())

            # 4. Lấy Lịch sử tìm kiếm -> Tìm SP tương ứng thêm vào Seed
            df_tim = pd.read_sql(
                f"SELECT TOP 3 TuKhoa FROM LichSuTimKiem WHERE NguoiDungID = {user_id} ORDER BY ThoiGian DESC", conn)
            for kw in df_tim['TuKhoa']:
                df_sp_tim = pd.read_sql(
                    f"SELECT TOP 3 ID FROM SanPham WHERE TenSanPham LIKE N'%{kw}%' AND TrangThai = 1", conn)
                seed_product_ids.update(df_sp_tim['ID'].tolist())

        elif session_id and session_id.lower() != 'null':
            # Khách vãng lai: Chỉ có Lịch sử xem và Tìm kiếm
            df_xem = pd.read_sql(
                f"SELECT TOP 10 SanPhamID FROM LichSuXem WHERE SessionID = '{session_id}' ORDER BY ThoiGian DESC", conn)
            seed_product_ids.update(df_xem['SanPhamID'].tolist())

            df_tim = pd.read_sql(
                f"SELECT TOP 3 TuKhoa FROM LichSuTimKiem WHERE SessionID = '{session_id}' ORDER BY ThoiGian DESC", conn)
            for kw in df_tim['TuKhoa']:
                df_sp_tim = pd.read_sql(
                    f"SELECT TOP 3 ID FROM SanPham WHERE TenSanPham LIKE N'%{kw}%' AND TrangThai = 1", conn)
                seed_product_ids.update(df_sp_tim['ID'].tolist())

        # Lưu lại những cái đã tương tác để lát nữa không recommend lại đồ cũ
        interacted_ids.update(seed_product_ids)

        # ---------------------------------------------------------
        # BƯỚC 2: XỬ LÝ COLD-START (KHÁCH MỚI TINH)
        # ---------------------------------------------------------
        if len(seed_product_ids) == 0:
            # Bài toán: Tự động tính các SP phù hợp (Top Lượt Xem)
            query_top = "SELECT TOP 8 ID, LuotXem FROM SanPham WHERE TrangThai = 1 ORDER BY LuotXem DESC"
            df_top = pd.read_sql(query_top, conn)
            conn.close()

            recommendations = []
            score = 1.0  # [SỬA LỖI] Bắt đầu bằng 1.0 cho SP có view cao nhất
            for index, row in df_top.iterrows():
                recommendations.append({
                    'SanPhamDuocGoiYID': int(row['ID']),
                    'DiemGoiY': score
                })
                score -= 0.01

            return jsonify({
                'success': True,
                'message': 'Cold start - Trả về Top Lượt Xem',
                'recommendations': recommendations
            })

        # ---------------------------------------------------------
        # BƯỚC 3: PHÂN TÍCH ĐẶC TRƯNG & TÌM SẢN PHẨM TƯƠNG TỰ
        # ---------------------------------------------------------
        query_all = """
            SELECT s.ID, s.TenSanPham, s.KieuDang, d.TenDanhMuc 
            FROM SanPham s
            LEFT JOIN DanhMuc d ON s.DanhMucID = d.ID
            WHERE s.TrangThai = 1
        """
        df_all = pd.read_sql(query_all, conn)
        conn.close()

        df_all['KieuDang'] = df_all['KieuDang'].fillna('')
        df_all['TenDanhMuc'] = df_all['TenDanhMuc'].fillna('')
        df_all['Features'] = df_all['TenSanPham'] + " " + df_all['KieuDang'] + " " + df_all['TenDanhMuc']

        vectorizer = TfidfVectorizer()
        tfidf_matrix = vectorizer.fit_transform(df_all['Features'])
        cosine_sim = cosine_similarity(tfidf_matrix, tfidf_matrix)

        product_scores = {}
        for v_id in seed_product_ids:
            if v_id in df_all['ID'].values:
                idx = df_all.index[df_all['ID'] == v_id][0]
                sim_scores = enumerate(cosine_sim[idx])

                for i, score in sim_scores:
                    candidate_id = int(df_all.iloc[i]['ID'])

                    # [QUAN TRỌNG]: Tìm mặt hàng TƯƠNG TỰ nhưng CHƯA ĐƯỢC XEM/MUA
                    if candidate_id not in interacted_ids:
                        if candidate_id not in product_scores:
                            product_scores[candidate_id] = 0
                        product_scores[candidate_id] += score

                        # Sắp xếp lấy Top 8 sản phẩm có tổng điểm cao nhất
        sorted_scores = sorted(product_scores.items(), key=lambda x: x[1], reverse=True)[:8]

        recommendations = []
        for item in sorted_scores:
            if item[1] > 0.05:
                recommendations.append({
                    'SanPhamDuocGoiYID': item[0],
                    'DiemGoiY': round(item[1], 4)
                })

        return jsonify({
            'success': True,
            'message': 'Personalized AI',
            'seed_count': len(seed_product_ids),
            'recommendations': recommendations
        })

    except Exception as e:
        print("LỖI USER RECOMMEND:", str(e))
        return jsonify({'success': False, 'error': str(e)})


# ==========================================
# [THÊM MỚI] PHẦN 4: AI GỢI Ý THƯỜNG MUA CÙNG NHAU (CROSS-SELL / CO-BUY)
# ==========================================

@app.route('/recommend_cobuy', methods=['GET'])
def recommend_cobuy():
    try:
        product_id = request.args.get('id')
        if not product_id:
            return jsonify({'success': False, 'message': 'Thiếu tham số id'})

        product_id = int(product_id)
        conn = get_db_connection()

        # THUẬT TOÁN: TÌM CÁC SẢN PHẨM KHÁC NẰM TRONG CÙNG ĐƠN HÀNG VỚI SẢN PHẨM NÀY
        query = f"""
            SELECT TOP 6
                bt_khac.SanPhamID as SanPhamDuocGoiYID,
                COUNT(DISTINCT ct_khac.DonHangID) as SoLanMuaChung
            FROM ChiTietDonHang ct_goc
            JOIN BienTheSanPham bt_goc ON ct_goc.BienTheSanPhamID = bt_goc.ID
            JOIN ChiTietDonHang ct_khac ON ct_goc.DonHangID = ct_khac.DonHangID
            JOIN BienTheSanPham bt_khac ON ct_khac.BienTheSanPhamID = bt_khac.ID
            JOIN SanPham sp_khac ON bt_khac.SanPhamID = sp_khac.ID
            WHERE bt_goc.SanPhamID = {product_id}
              AND bt_khac.SanPhamID != {product_id}
              AND sp_khac.TrangThai = 1
            GROUP BY bt_khac.SanPhamID
            ORDER BY SoLanMuaChung DESC
        """

        df = pd.read_sql(query, conn)
        conn.close()

        recommendations = []
        if not df.empty:
            # Lấy số lần mua chung cao nhất để làm chuẩn (Max = 1.0 điểm)
            max_count = df['SoLanMuaChung'].max()

            for index, row in df.iterrows():
                # Nếu max_count = 5, sản phẩm mua chung 5 lần được 1.0 điểm, 4 lần được 0.8 điểm...
                score = float(row['SoLanMuaChung']) / float(max_count)

                recommendations.append({
                    'SanPhamDuocGoiYID': int(row['SanPhamDuocGoiYID']),
                    'DiemGoiY': round(score, 4)
                })

        return jsonify({
            'success': True,
            'product_id': product_id,
            'message': 'Co-buy recommendations generated',
            'recommendations': recommendations
        })

    except Exception as e:
        print("LỖI CO-BUY RECOMMEND:", str(e))
        return jsonify({'success': False, 'error': str(e)})


# ==========================================
# [THÊM MỚI] PHẦN 5: AI PHÂN TÍCH INSIGHT CHO ADMIN (DASHBOARD)
# ==========================================

@app.route('/admin_insights', methods=['GET'])
def admin_insights():
    try:
        conn = get_db_connection()

        # 1. PHÂN TÍCH PHỄU (Drop-off Analysis): Sản phẩm xem nhiều nhưng KHÔNG ai mua
        # -> Insight: Có thể do giá quá cao, ảnh xấu, hoặc hết size phổ biến. Cần flash sale hoặc review lại.
        query_pheu = """
            SELECT TOP 10
                s.ID, 
                s.TenSanPham, 
                s.LuotXem, 
                ISNULL(SUM(ct.SoLuong), 0) as TongDaBan
            FROM SanPham s
            LEFT JOIN BienTheSanPham bt ON s.ID = bt.SanPhamID
            LEFT JOIN ChiTietDonHang ct ON bt.ID = ct.BienTheSanPhamID
            WHERE s.TrangThai = 1
            GROUP BY s.ID, s.TenSanPham, s.LuotXem
            HAVING s.LuotXem > 10 AND ISNULL(SUM(ct.SoLuong), 0) = 0
            ORDER BY s.LuotXem DESC
        """
        df_pheu = pd.read_sql(query_pheu, conn)

        # 2. PHÂN TÍCH XU HƯỚNG (Trend Analysis): Từ khóa được tìm kiếm nhiều nhất
        # -> Insight: Biết khách đang săn lùng món gì để nhập thêm hàng hoặc làm SEO.
        query_trend = """
            SELECT TOP 5 
                TuKhoa, 
                COUNT(*) as SoLuotTim
            FROM LichSuTimKiem
            GROUP BY TuKhoa
            ORDER BY SoLuotTim DESC
        """
        df_trend = pd.read_sql(query_trend, conn)

        # 3. PHÂN TÍCH TỶ LỆ CHUYỂN ĐỔI (Conversion Rate): Sản phẩm bán chạy nhất
        query_bestseller = """
            SELECT TOP 5
                s.ID,
                s.TenSanPham,
                ISNULL(SUM(ct.SoLuong), 0) as TongDaBan
            FROM SanPham s
            JOIN BienTheSanPham bt ON s.ID = bt.SanPhamID
            JOIN ChiTietDonHang ct ON bt.ID = ct.BienTheSanPhamID
            JOIN DonHang d ON ct.DonHangID = d.ID
            WHERE d.TrangThaiDonHang = 3 -- Chỉ tính đơn đã giao thành công
            GROUP BY s.ID, s.TenSanPham
            ORDER BY TongDaBan DESC
        """
        df_bestseller = pd.read_sql(query_bestseller, conn)

        conn.close()

        return jsonify({
            'success': True,
            'warning_products': df_pheu.to_dict('records'),
            'trending_keywords': df_trend.to_dict('records'),
            'best_sellers': df_bestseller.to_dict('records')
        })

    except Exception as e:
        print("LỖI ADMIN INSIGHTS:", str(e))
        return jsonify({'success': False, 'error': str(e)})











# ==========================================
# CHẠY SERVER
# ==========================================
if __name__ == '__main__':
    app.run(port=5000, debug=True, use_reloader=False)