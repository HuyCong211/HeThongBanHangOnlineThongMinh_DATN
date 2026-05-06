import os
import time
import threading
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
# PHẦN 1: TÌM KIẾM BẰNG HÌNH ẢNH
# ==========================================

# --- CẤU HÌNH ---
IMAGE_FOLDER = 'static/images'

# Load Model (Chỉ lấy phần trích xuất đặc trưng)
print("--- DANG KHOI DONG AI... ---")
model = MobileNetV2(weights='imagenet', include_top=False, pooling='avg')
print("--- MODEL DA SAN SANG! ---")

# Biến lưu trữ data trong RAM
_index_lock = threading.Lock()
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
        print(f"Lỗi extract_feature: {e}")
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
        feat = extract_feature(img_path)
        if feat is not None:
            # [FIX] Dùng lock khi ghi vào shared list
            with _index_lock:
                features_db.append(feat)
                image_names_db.append(img_name)
            count += 1
            if count % 50 == 0:
                print(f"Da hoc xong {count} anh...")

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
        query_vector = extract_feature(file.stream)
        if query_vector is None:
            return jsonify({'success': False, 'message': 'Ảnh lỗi không đọc được'})

        query_vector = query_vector.reshape(1, -1)

        # [FIX] Dùng lock khi đọc shared list để tránh race condition
        with _index_lock:
            if len(features_db) == 0:
                return jsonify({'success': False, 'message': 'Kho ảnh rỗng.'})
            local_features = list(features_db)
            local_names = list(image_names_db)

        similarities = cosine_similarity(query_vector, local_features)
        top_indices = np.argsort(similarities[0])[::-1][:10]

        results = []
        for i in top_indices:
            score = similarities[0][i]
            if score > 0.5:
                results.append({
                    'image_name': local_names[i],
                    'score': float(score)
                })

        return jsonify({'success': True, 'matches': results})

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
    image_name = request.form.get('image_name')

    if not image_name:
        return jsonify({'success': False, 'message': 'Thiếu tên file (image_name)'})

    try:
        if not os.path.exists(IMAGE_FOLDER):
            os.makedirs(IMAGE_FOLDER)
        save_path = os.path.join(IMAGE_FOLDER, image_name)
        file.save(save_path)

        feat = extract_feature(save_path)
        if feat is not None:
            # [FIX] Thread-safe append
            with _index_lock:
                features_db.append(feat)
                image_names_db.append(image_name)
            return jsonify({'success': True, 'message': f'Đã đồng bộ {image_name} vào AI'})
        else:
            return jsonify({'success': False, 'message': 'Lỗi trích xuất vector AI'})

    except Exception as e:
        print("LỖI ĐỒNG BỘ AI:", str(e))
        return jsonify({'success': False, 'message': str(e)})


# ==========================================
# PHẦN 2: AI GỢI Ý SẢN PHẨM TƯƠNG TỰ (TEXT-BASED)
# ==========================================

# --- HÀM KẾT NỐI DATABASE SQL SERVER ---
# ==========================================
# CẤU HÌNH DATABASE
# ==========================================

DB_CONN_STR = (
    r'DRIVER={ODBC Driver 17 for SQL Server};'
    r'SERVER=DESKTOP-PC8H4EK\SQLEXPRESS;'
    r'DATABASE=DoAnTotNghiep_Test;'
    r'Trusted_Connection=yes;'
)


def get_db_connection():
    return pyodbc.connect(DB_CONN_STR)


# ==========================================
# CATALOG CACHE — tránh query DB mỗi request
# ==========================================

_catalog_cache = {'df': None, 'ts': 0, 'vectorizer': None, 'tfidf_matrix': None}
CATALOG_TTL = 300  # 5 phút


def _build_tfidf(df: pd.DataFrame):
    """Xây TF-IDF matrix từ toàn bộ đặc trưng văn bản của sản phẩm."""
    corpus = (
        df['Tags'].fillna('') + ' ' +
        df['TenDanhMuc'].fillna('') + ' ' +
        df['KieuDang'].fillna('') + ' ' +
        df['ChatLieu'].fillna('') + ' ' +
        df['MauSacHienCo'].fillna('') + ' ' +
        df['KichThuocHienCo'].fillna('')
    )
    vectorizer = TfidfVectorizer(max_features=500)
    matrix = vectorizer.fit_transform(corpus)
    return vectorizer, matrix


def load_catalog_cached(conn) -> pd.DataFrame:
    """Load catalog từ DB, cache lại trong RAM 5 phút."""
    now = time.time()
    if _catalog_cache['df'] is not None and (now - _catalog_cache['ts']) < CATALOG_TTL:
        return _catalog_cache['df']

    # [FIX] Thêm MauSacHienCo, KichThuocHienCo vào query
    query = """
        SELECT
            v.SanPhamID AS ID,
            v.TenSanPham,
            v.TenDanhMuc,
            v.KieuDang,
            v.ChatLieu,
            v.GiaBan,
            v.MauSacHienCo,
            v.KichThuocHienCo,
            s.Tags
        FROM v_AI_SanPham v
        JOIN SanPham s ON v.SanPhamID = s.ID
    """
    df = pd.read_sql(query, conn)
    for col in ['TenDanhMuc', 'KieuDang', 'ChatLieu', 'Tags', 'MauSacHienCo', 'KichThuocHienCo']:
        df[col] = df[col].fillna('')
    df['GiaBan'] = pd.to_numeric(df['GiaBan'], errors='coerce').fillna(0)

    vectorizer, tfidf_matrix = _build_tfidf(df)

    _catalog_cache['df'] = df
    _catalog_cache['ts'] = now
    _catalog_cache['vectorizer'] = vectorizer
    _catalog_cache['tfidf_matrix'] = tfidf_matrix

    return df


# ==========================================
# TRỌNG SỐ ĐẶC TRƯNG
# [FIX] Thêm MauSac + KichThuoc, điều chỉnh lại tỷ lệ
# ==========================================

WEIGHTS = {
    'DanhMuc':    0.25,
    'KieuDang':   0.20,
    'ChatLieu':   0.15,
    'Tags':       0.15,   # Dùng TF-IDF cosine thay Jaccard
    'MauSac':     0.12,   # [MỚI] Màu sắc quan trọng trong thời trang
    'KichThuoc':  0.05,   # [MỚI] Kích thước ít ưu tiên hơn vì nhiều người mặc nhiều size
    'GiaBan':     0.08,
}


def compute_similarity_scores_vectorized(
    target_idx: int,
    df_candidates: pd.DataFrame,
    candidates_idx: list,
    max_price: float
) -> np.ndarray:
    """
    Tính điểm tương đồng giữa sản phẩm mục tiêu và danh sách ứng viên.

    [FIX] Dùng TF-IDF cosine similarity cho Tags thay vì Jaccard thuần.
    [FIX] Bổ sung MauSac và KichThuoc vào scoring.
    """
    target = _catalog_cache['df'].iloc[target_idx]
    df_all = _catalog_cache['df']
    tfidf_matrix = _catalog_cache['tfidf_matrix']

    # --- DanhMuc: exact match ---
    s_danhmuc = (df_candidates['TenDanhMuc'] == target['TenDanhMuc']).astype(float)

    # --- Jaccard cho KieuDang, ChatLieu, MauSac, KichThuoc ---
    def jaccard_series(series, target_val):
        if not target_val:
            return pd.Series(0.0, index=series.index)
        target_set = set(str(target_val).lower().replace(',', ' ').split())

        def _jaccard(text):
            if not text:
                return 0.0
            text_set = set(str(text).lower().replace(',', ' ').split())
            inter = len(target_set & text_set)
            union = len(target_set | text_set)
            return inter / union if union > 0 else 0.0

        return series.apply(_jaccard)

    s_kieudang  = jaccard_series(df_candidates['KieuDang'],      target['KieuDang'])
    s_chatlieu  = jaccard_series(df_candidates['ChatLieu'],      target['ChatLieu'])
    s_mausac    = jaccard_series(df_candidates['MauSacHienCo'],  target['MauSacHienCo'])
    s_kichthuoc = jaccard_series(df_candidates['KichThuocHienCo'], target['KichThuocHienCo'])

    # --- [FIX] TF-IDF cosine cho Tags ---
    target_vec = tfidf_matrix[target_idx]
    candidate_vecs = tfidf_matrix[candidates_idx]
    s_tags_raw = cosine_similarity(target_vec, candidate_vecs).flatten()
    s_tags = pd.Series(s_tags_raw, index=df_candidates.index)

    # --- Giá: linear decay trong ±50% ---
    target_price = float(target['GiaBan'] or 0)
    prices = df_candidates['GiaBan'].astype(float)
    cutoff = max_price * 0.5
    delta = (prices - target_price).abs()
    s_gia = np.maximum(0.0, 1.0 - (delta / cutoff))

    total = (
        WEIGHTS['DanhMuc']   * s_danhmuc.values +
        WEIGHTS['KieuDang']  * s_kieudang.values +
        WEIGHTS['ChatLieu']  * s_chatlieu.values +
        WEIGHTS['Tags']      * s_tags.values +
        WEIGHTS['MauSac']    * s_mausac.values +
        WEIGHTS['KichThuoc'] * s_kichthuoc.values +
        WEIGHTS['GiaBan']    * s_gia.values
    )
    return total


# ==========================================
# PHẦN 2: GỢI Ý SẢN PHẨM TƯƠNG TỰ (ITEM-BASED)
# ==========================================

@app.route('/recommend_similar', methods=['GET'])
def recommend_similar():
    try:
        product_id = request.args.get('id')
        if not product_id:
            return jsonify({'success': False, 'message': 'Thiếu tham số id'})

        product_id = int(product_id)
        conn = get_db_connection()
        df = load_catalog_cached(conn)
        conn.close()

        if df.empty or product_id not in df['ID'].values:
            return jsonify({'success': False, 'message': 'Không tìm thấy sản phẩm'})

        target_idx = df.index[df['ID'] == product_id][0]
        candidates_mask = df['ID'] != product_id
        df_candidates = df[candidates_mask].copy()
        candidates_idx = df_candidates.index.tolist()

        max_price = df['GiaBan'].max() if df['GiaBan'].max() > 0 else 1.0
        scores = compute_similarity_scores_vectorized(
            target_idx, df_candidates, candidates_idx, max_price
        )
        df_candidates = df_candidates.copy()
        df_candidates['Score'] = scores

        top = df_candidates[df_candidates['Score'] > 0.1].nlargest(8, 'Score')
        recommendations = [
            {'SanPhamDuocGoiYID': int(r['ID']), 'DiemGoiY': round(r['Score'], 4)}
            for _, r in top.iterrows()
        ]

        return jsonify({
            'success': True,
            'product_id': product_id,
            'recommendations': recommendations
        })

    except Exception as e:
        return jsonify({'success': False, 'error': str(e)})


# ==========================================
# PHẦN 3: GỢI Ý CÁ NHÂN HÓA (HYBRID RECOMMENDER)
# ==========================================

# ----------------------------------------------------------
# COLD-START HELPER — dùng cho khách vãng lai (chưa đăng nhập,
# chưa có session, hoặc session chưa có hành vi nào)
# ----------------------------------------------------------
_cold_cache = {'data': None, 'ts': 0}
COLD_CACHE_TTL = 600  # 10 phút — cold-start không cần real-time


def build_cold_start_recommendations(conn, top_n: int = 8):
    """
    Xây danh sách gợi ý cho khách vãng lai bằng cách MIX nhiều tín hiệu:
      1) Best-seller     — sản phẩm bán chạy nhất (đơn đã giao TrangThaiDonHang = 3)
      2) Trending 30d    — sản phẩm có LuotXem cao trong 30 ngày gần đây (proxy: NgayCapNhat)
      3) New arrivals    — sản phẩm mới ra (NgayTao gần đây)
    Sau đó áp dụng diversity (mỗi danh mục tối đa 2 SP) để tránh trả về toàn 1 loại.
    Có cache 10 phút để giảm tải DB.
    """
    now = time.time()
    if _cold_cache['data'] is not None and (now - _cold_cache['ts']) < COLD_CACHE_TTL:
        return _cold_cache['data'][:top_n]

    pool = {}  # SanPhamID -> {'score': float, 'cat': int}

    # ---------- 1) BEST-SELLER ----------
    try:
        df_bs = pd.read_sql("""
            SELECT TOP 30
                s.ID, s.DanhMucID,
                ISNULL(SUM(ct.SoLuong), 0) AS TongDaBan
            FROM SanPham s
            LEFT JOIN BienTheSanPham  bt ON s.ID  = bt.SanPhamID
            LEFT JOIN ChiTietDonHang  ct ON bt.ID = ct.BienTheSanPhamID
            LEFT JOIN DonHang         d  ON ct.DonHangID = d.ID AND d.TrangThaiDonHang = 3
            WHERE s.TrangThai = 1
            GROUP BY s.ID, s.DanhMucID
            ORDER BY TongDaBan DESC
        """, conn)
        if not df_bs.empty:
            mx = max(df_bs['TongDaBan'].max(), 1)
            for _, r in df_bs.iterrows():
                pid = int(r['ID'])
                pool[pid] = {
                    'score': pool.get(pid, {}).get('score', 0) + 0.50 * (r['TongDaBan'] / mx),
                    'cat': int(r['DanhMucID']) if pd.notna(r['DanhMucID']) else 0,
                }
    except Exception as e:
        print("[COLD-START] best-seller error:", e)

    # ---------- 2) TRENDING (LuotXem cao) ----------
    try:
        df_tr = pd.read_sql("""
            SELECT TOP 30 ID, DanhMucID, ISNULL(LuotXem, 0) AS LuotXem
            FROM SanPham
            WHERE TrangThai = 1
            ORDER BY LuotXem DESC, NgayCapNhat DESC
        """, conn)
        if not df_tr.empty:
            mx = max(df_tr['LuotXem'].max(), 1)
            for _, r in df_tr.iterrows():
                pid = int(r['ID'])
                pool[pid] = {
                    'score': pool.get(pid, {}).get('score', 0) + 0.30 * (r['LuotXem'] / mx),
                    'cat': int(r['DanhMucID']) if pd.notna(r['DanhMucID']) else 0,
                }
    except Exception as e:
        print("[COLD-START] trending error:", e)

    # ---------- 3) NEW ARRIVALS ----------
    try:
        df_new = pd.read_sql("""
            SELECT TOP 20 ID, DanhMucID, NgayTao
            FROM SanPham
            WHERE TrangThai = 1
            ORDER BY NgayTao DESC
        """, conn)
        if not df_new.empty:
            n = len(df_new)
            for i, r in df_new.iterrows():
                pid = int(r['ID'])
                # Sản phẩm mới nhất có điểm cao hơn (decay tuyến tính)
                fresh_score = (n - i) / n
                pool[pid] = {
                    'score': pool.get(pid, {}).get('score', 0) + 0.20 * fresh_score,
                    'cat': int(r['DanhMucID']) if pd.notna(r['DanhMucID']) else 0,
                }
    except Exception as e:
        print("[COLD-START] new arrivals error:", e)

    # ---------- DIVERSITY: mỗi danh mục tối đa 2 SP ----------
    sorted_items = sorted(pool.items(), key=lambda x: x[1]['score'], reverse=True)

    cat_count = {}
    final_list = []
    MAX_PER_CAT = 2
    for pid, info in sorted_items:
        cat = info['cat']
        if cat_count.get(cat, 0) >= MAX_PER_CAT:
            continue
        final_list.append((pid, info['score']))
        cat_count[cat] = cat_count.get(cat, 0) + 1
        if len(final_list) >= top_n * 2:  # lấy dư để có buffer
            break

    # Nếu chưa đủ top_n (do diversity quá nghiêm), bổ sung tiếp từ pool
    if len(final_list) < top_n:
        chosen_ids = {p for p, _ in final_list}
        for pid, info in sorted_items:
            if pid not in chosen_ids:
                final_list.append((pid, info['score']))
                if len(final_list) >= top_n:
                    break

    # ---------- FALLBACK CUỐI CÙNG: nếu pool RỖNG (shop mới toanh) ----------
    if not final_list:
        try:
            df_fb = pd.read_sql("""
                SELECT TOP (?) ID FROM SanPham
                WHERE TrangThai = 1
                ORDER BY NgayTao DESC, ID DESC
            """, conn, params=[top_n])
            final_list = [(int(r['ID']), 1.0 - i * 0.01) for i, r in df_fb.iterrows()]
        except Exception as e:
            print("[COLD-START] fallback error:", e)

    recs = [
        {'SanPhamDuocGoiYID': int(pid), 'DiemGoiY': round(float(score), 4)}
        for pid, score in final_list[:top_n]
    ]

    _cold_cache['data'] = recs
    _cold_cache['ts'] = now
    return recs


# ----------------------------------------------------------
# ENDPOINT RIÊNG cho khách vãng lai — frontend gọi khi user
# CHƯA đăng nhập và CHƯA có hành vi nào (trang chủ lần đầu).
# Nhanh hơn /recommend_user vì bỏ qua toàn bộ logic seed/CBF/CF.
# ----------------------------------------------------------
@app.route('/recommend_guest', methods=['GET'])
def recommend_guest():
    try:
        top_n = int(request.args.get('top', 8))
        top_n = max(1, min(top_n, 24))  # clamp 1..24
        conn = get_db_connection()
        recs = build_cold_start_recommendations(conn, top_n=top_n)
        conn.close()
        return jsonify({
            'success': True,
            'message': 'Guest recommendations (cold-start)',
            'is_cold_start': True,
            'recommendations': recs
        })
    except Exception as e:
        print("LỖI RECOMMEND_GUEST:", str(e))
        return jsonify({'success': False, 'error': str(e), 'recommendations': []})


@app.route('/recommend_user', methods=['GET'])
def recommend_user():
    try:
        user_id_raw = request.args.get('user_id')
        session_id  = request.args.get('session_id', '')

        conn = get_db_connection()

        user_id = None
        if user_id_raw and user_id_raw.lower() not in ('null', ''):
            try:
                user_id = int(user_id_raw)
            except ValueError:
                pass

        # ----------------------------------------------------------
        # LUỒNG 1: XÂY DỰNG SEED TỪ HÀNH VI NGƯỜI DÙNG
        # [FIX] Dùng parameterized query — ngăn SQL Injection
        # [FIX] Thêm DanhGia (ratings) vào seed với trọng số cao
        # ----------------------------------------------------------
        seed_weighted = {}

        if user_id:
            query_seeds = """
                SELECT SanPhamID, CAST(MAX(Weight) AS FLOAT) AS BehaviorWeight
                FROM (
                    SELECT TOP 10 SanPhamID, 1.0 AS Weight
                    FROM LichSuXem
                    WHERE NguoiDungID = ?

                    UNION ALL

                    SELECT TOP 10 SanPhamID, 2.0 AS Weight
                    FROM SanPhamYeuThich
                    WHERE NguoiDungID = ?

                    UNION ALL

                    SELECT TOP 10 b.SanPhamID,
                        CASE WHEN dg.SoSao >= 4 THEN 4.0 ELSE 2.5 END AS Weight
                    FROM DanhGia dg
                    JOIN SanPham sp ON dg.SanPhamID = sp.ID
                    JOIN BienTheSanPham b ON b.SanPhamID = sp.ID
                    WHERE dg.NguoiDungID = ?
                      AND dg.TrangThai = 1

                    UNION ALL

                    SELECT TOP 10 b.SanPhamID, 3.0 AS Weight
                    FROM ChiTietDonHang c
                    JOIN DonHang d ON c.DonHangID = d.ID
                    JOIN BienTheSanPham b ON c.BienTheSanPhamID = b.ID
                    WHERE d.NguoiDungID = ?
                ) AS T
                GROUP BY SanPhamID
            """
            df_seeds = pd.read_sql(query_seeds, conn,
                                   params=[user_id, user_id, user_id, user_id])
            seed_weighted = dict(zip(df_seeds['SanPhamID'], df_seeds['BehaviorWeight']))

        elif session_id and session_id.lower() != 'null':
            query_seeds = """
                SELECT SanPhamID, CAST(MAX(Weight) AS FLOAT) AS BehaviorWeight
                FROM (
                    SELECT TOP 10 SanPhamID, 1.0 AS Weight
                    FROM LichSuXem
                    WHERE SessionID = ?
                ) AS T
                GROUP BY SanPhamID
            """
            df_seeds = pd.read_sql(query_seeds, conn, params=[session_id])
            seed_weighted = dict(zip(df_seeds['SanPhamID'], df_seeds['BehaviorWeight']))

        # Lịch sử tìm kiếm — [FIX] parameterized + xử lý chuỗi 'null'
        if user_id:
            df_search = pd.read_sql(
                "SELECT TOP 3 TuKhoa FROM LichSuTimKiem WHERE NguoiDungID = ? ORDER BY ThoiGian DESC",
                conn, params=[user_id]
            )
        elif session_id and session_id.lower() != 'null':
            df_search = pd.read_sql(
                "SELECT TOP 3 TuKhoa FROM LichSuTimKiem WHERE SessionID = ? ORDER BY ThoiGian DESC",
                conn, params=[session_id]
            )
        else:
            df_search = pd.DataFrame()

        if not df_search.empty:
            for kw in df_search['TuKhoa']:
                df_sp = pd.read_sql(
                    "SELECT TOP 10 ID FROM SanPham WHERE TrangThai = 1 AND TenSanPham LIKE ?",
                    conn, params=[f'%{kw}%']
                )
                for pid in df_sp['ID']:
                    if pid not in seed_weighted:
                        seed_weighted[pid] = 1.0

        # ----------------------------------------------------------
        # COLD-START: Khách vãng lai / khách mới chưa có hành vi
        # [FIX] Mix nhiều tín hiệu cho gợi ý đa dạng & ổn định:
        #   - Best-seller (đơn đã giao thành công)
        #   - Trending 30 ngày (LuotXem cao gần đây)
        #   - New arrivals (NgayTao gần đây)
        #   - Diversity theo danh mục (mỗi danh mục lấy tối đa 2 SP)
        #   - Fallback: nếu shop chưa có dữ liệu, lấy theo LuotXem / NgayTao
        # ----------------------------------------------------------
        if not seed_weighted:
            print(f"[COLD-START] user_id={user_id}, session_id='{session_id}' — fallback to popular products")
            cold_recs = build_cold_start_recommendations(conn, top_n=8)
            conn.close()
            return jsonify({
                'success': True,
                'message': 'Cold start — Best Sellers + Trending + New Arrivals (diverse)',
                'is_cold_start': True,
                'recommendations': cold_recs
            })

        # ----------------------------------------------------------
        # LUỒNG 2: CONTENT-BASED FILTERING (CBF)
        # ----------------------------------------------------------
        df_all = load_catalog_cached(conn)
        interacted_ids = set(seed_weighted.keys())
        df_candidates = df_all[~df_all['ID'].isin(interacted_ids)].copy()

        cb_scores = {}
        if not df_candidates.empty:
            max_price = df_all['GiaBan'].max() if df_all['GiaBan'].max() > 0 else 1.0
            total_scores = np.zeros(len(df_candidates))

            for seed_id, weight in seed_weighted.items():
                matches = df_all.index[df_all['ID'] == seed_id]
                if len(matches) == 0:
                    continue
                target_idx = matches[0]
                candidates_idx = df_candidates.index.tolist()
                sim = compute_similarity_scores_vectorized(
                    target_idx, df_candidates, candidates_idx, max_price
                )
                total_scores += np.where(sim > 0.1, sim * weight, 0)

            df_candidates = df_candidates.copy()
            df_candidates['CB_Score'] = total_scores
            max_cb = df_candidates['CB_Score'].max()
            if max_cb > 0:
                df_candidates['CB_Score'] /= max_cb

            cb_scores = dict(zip(df_candidates['ID'], df_candidates['CB_Score']))

        # ----------------------------------------------------------
        # LUỒNG 3: COLLABORATIVE FILTERING (CF) — chỉ cho user đăng nhập
        # [FIX] Parameterized query — ngăn SQL Injection
        # [FIX] Thêm DanhGia vào signal CF
        # ----------------------------------------------------------
        cf_scores = {}
        if user_id:
            query_cf = """
                WITH MyInteractions AS (
                    SELECT SanPhamID FROM LichSuXem        WHERE NguoiDungID = ?
                    UNION
                    SELECT SanPhamID FROM SanPhamYeuThich  WHERE NguoiDungID = ?
                    UNION
                    SELECT SanPhamID FROM DanhGia          WHERE NguoiDungID = ? AND TrangThai = 1
                    UNION
                    SELECT b.SanPhamID
                    FROM ChiTietDonHang c
                    JOIN DonHang d ON c.DonHangID = d.ID
                    JOIN BienTheSanPham b ON c.BienTheSanPhamID = b.ID
                    WHERE d.NguoiDungID = ?
                ),
                SimilarUsers AS (
                    SELECT NguoiDungID, COUNT(DISTINCT SanPhamID) AS SimilarityScore
                    FROM (
                        SELECT NguoiDungID, SanPhamID FROM LichSuXem
                            WHERE NguoiDungID != ?
                        UNION ALL
                        SELECT NguoiDungID, SanPhamID FROM SanPhamYeuThich
                            WHERE NguoiDungID != ?
                        UNION ALL
                        SELECT NguoiDungID, SanPhamID FROM DanhGia
                            WHERE NguoiDungID != ? AND TrangThai = 1
                        UNION ALL
                        SELECT d.NguoiDungID, b.SanPhamID
                        FROM ChiTietDonHang c
                        JOIN DonHang d ON c.DonHangID = d.ID
                        JOIN BienTheSanPham b ON c.BienTheSanPhamID = b.ID
                        WHERE d.NguoiDungID != ?
                    ) AllOther
                    WHERE SanPhamID IN (SELECT SanPhamID FROM MyInteractions)
                    GROUP BY NguoiDungID
                )
                SELECT TOP 20
                    a.SanPhamID,
                    CAST(SUM(s.SimilarityScore) AS FLOAT) AS CF_Score
                FROM (
                    SELECT NguoiDungID, SanPhamID FROM LichSuXem       WHERE NguoiDungID != ?
                    UNION
                    SELECT NguoiDungID, SanPhamID FROM SanPhamYeuThich WHERE NguoiDungID != ?
                    UNION
                    SELECT NguoiDungID, SanPhamID FROM DanhGia
                        WHERE NguoiDungID != ? AND TrangThai = 1
                    UNION
                    SELECT d.NguoiDungID, b.SanPhamID
                    FROM ChiTietDonHang c
                    JOIN DonHang d ON c.DonHangID = d.ID
                    JOIN BienTheSanPham b ON c.BienTheSanPhamID = b.ID
                    WHERE d.NguoiDungID != ?
                ) a
                JOIN SimilarUsers s ON a.NguoiDungID = s.NguoiDungID
                WHERE a.SanPhamID NOT IN (SELECT SanPhamID FROM MyInteractions)
                GROUP BY a.SanPhamID
                ORDER BY CF_Score DESC
            """
            params_cf = [
                user_id, user_id, user_id, user_id,   # MyInteractions
                user_id, user_id, user_id, user_id,   # SimilarUsers subquery
                user_id, user_id, user_id, user_id,   # outer SELECT a
            ]
            df_cf = pd.read_sql(query_cf, conn, params=params_cf)
            if not df_cf.empty:
                max_cf = df_cf['CF_Score'].max()
                if max_cf > 0:
                    df_cf['CF_Score'] /= max_cf
                cf_scores = dict(zip(df_cf['SanPhamID'], df_cf['CF_Score']))

        conn.close()

        # ----------------------------------------------------------
        # LUỒNG 4: HYBRID MERGING — trộn CBF + CF
        # ----------------------------------------------------------
        CB_WEIGHT = 0.65
        CF_WEIGHT = 0.35

        all_candidate_ids = set(cb_scores.keys()) | set(cf_scores.keys())
        final_scores = {}
        for pid in all_candidate_ids:
            s_cb = cb_scores.get(pid, 0)
            s_cf = cf_scores.get(pid, 0)
            if not user_id:
                final_scores[pid] = s_cb
            else:
                final_scores[pid] = (s_cb * CB_WEIGHT) + (s_cf * CF_WEIGHT)

        sorted_recs = sorted(
            [(pid, score) for pid, score in final_scores.items() if score > 0],
            key=lambda x: x[1], reverse=True
        )[:8]

        recommendations = [
            {'SanPhamDuocGoiYID': int(pid), 'DiemGoiY': round(score, 4)}
            for pid, score in sorted_recs
        ]

        return jsonify({
            'success': True,
            'message': 'Hybrid AI — CBF + Collaborative Filtering',
            'hybrid_info': 'Content-Based: 65%, Collaborative: 35%',
            'seed_count': len(seed_weighted),
            'recommendations': recommendations
        })

    except Exception as e:
        print("LỖI USER RECOMMEND:", str(e))
        return jsonify({'success': False, 'error': str(e)})


# ==========================================
# PHẦN 4: GỢI Ý THƯỜNG MUA CÙNG NHAU (CO-BUY)
# ==========================================

COBUY_CACHE = {}
CACHE_TTL = 3600  # 1 giờ


@app.route('/recommend_cobuy', methods=['GET'])
def recommend_cobuy():
    try:
        product_id = request.args.get('id')
        if not product_id:
            return jsonify({'success': False, 'message': 'Thiếu tham số id'})
        product_id = int(product_id)

        now = time.time()
        if product_id in COBUY_CACHE:
            cached_time, cached_data = COBUY_CACHE[product_id]
            if now - cached_time < CACHE_TTL:
                return jsonify({'success': True, 'message': 'Loaded from Cache',
                                'recommendations': cached_data})

        conn = get_db_connection()
        # [FIX] Dùng parameterized query
        query = """
            SELECT TOP 6
                bt_khac.SanPhamID AS SanPhamDuocGoiYID,
                COUNT(DISTINCT ct_khac.DonHangID) AS SoLanMuaChung
            FROM ChiTietDonHang ct_goc
            JOIN BienTheSanPham bt_goc  ON ct_goc.BienTheSanPhamID = bt_goc.ID
            JOIN ChiTietDonHang ct_khac ON ct_goc.DonHangID = ct_khac.DonHangID
            JOIN BienTheSanPham bt_khac ON ct_khac.BienTheSanPhamID = bt_khac.ID
            JOIN SanPham sp_khac        ON bt_khac.SanPhamID = sp_khac.ID
            WHERE bt_goc.SanPhamID = ?
              AND bt_khac.SanPhamID != ?
              AND sp_khac.TrangThai = 1
            GROUP BY bt_khac.SanPhamID
            ORDER BY SoLanMuaChung DESC
        """
        df = pd.read_sql(query, conn, params=[product_id, product_id])
        conn.close()

        recommendations = []
        if not df.empty:
            max_count = df['SoLanMuaChung'].max()
            recommendations = [
                {
                    'SanPhamDuocGoiYID': int(r['SanPhamDuocGoiYID']),
                    'DiemGoiY': round(r['SoLanMuaChung'] / max_count, 4)
                }
                for _, r in df.iterrows()
            ]

        COBUY_CACHE[product_id] = (now, recommendations)
        return jsonify({'success': True, 'message': 'Generated via SQL',
                        'recommendations': recommendations})

    except Exception as e:
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