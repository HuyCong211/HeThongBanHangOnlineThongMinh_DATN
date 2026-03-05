using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Linq;

namespace DoAn_Ver2.Models
{
    public partial class DoAn_DbModel : DbContext
    {
        public DoAn_DbModel()
            : base("name=DoAn_DbModel")
        {
            this.Configuration.LazyLoadingEnabled = false;
            this.Configuration.ProxyCreationEnabled = false;
        }

        public virtual DbSet<AnhSanPham> AnhSanPhams { get; set; }
        public virtual DbSet<BienTheSanPham> BienTheSanPhams { get; set; }
        public virtual DbSet<CauHinhChung> CauHinhChungs { get; set; }
        public virtual DbSet<ChiTietDonHang> ChiTietDonHangs { get; set; }
        public virtual DbSet<DanhGia> DanhGias { get; set; }
        public virtual DbSet<DanhMuc> DanhMucs { get; set; }
        public virtual DbSet<DiaChi> DiaChis { get; set; }
        public virtual DbSet<DonHang> DonHangs { get; set; }
        public virtual DbSet<GioHang> GioHangs { get; set; }
        public virtual DbSet<GioHangChiTiet> GioHangChiTiets { get; set; }
        public virtual DbSet<GoiYSanPham> GoiYSanPhams { get; set; }
        public virtual DbSet<KichThuoc> KichThuocs { get; set; }
        public virtual DbSet<LichSuKho> LichSuKhoes { get; set; }
        public virtual DbSet<LichSuTimKiem> LichSuTimKiems { get; set; }
        public virtual DbSet<LichSuXem> LichSuXems { get; set; }
        public virtual DbSet<MaGiamGia> MaGiamGias { get; set; }
        public virtual DbSet<MauSac> MauSacs { get; set; }
        public virtual DbSet<NguoiDung> NguoiDungs { get; set; }
        public virtual DbSet<PhuongThucThanhToan> PhuongThucThanhToans { get; set; }
        public virtual DbSet<SanPham> SanPhams { get; set; }
        public virtual DbSet<SanPhamYeuThich> SanPhamYeuThiches { get; set; }
        public virtual DbSet<TinTuc> TinTucs { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BienTheSanPham>()
                .HasMany(e => e.ChiTietDonHangs)
                .WithRequired(e => e.BienTheSanPham)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<BienTheSanPham>()
                .HasMany(e => e.GioHangChiTiets)
                .WithRequired(e => e.BienTheSanPham)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<BienTheSanPham>()
                .HasMany(e => e.LichSuKhoes)
                .WithRequired(e => e.BienTheSanPham)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<DanhMuc>()
                .HasMany(e => e.DanhMuc1)
                .WithOptional(e => e.DanhMuc2)
                .HasForeignKey(e => e.DanhMucChaID);

            modelBuilder.Entity<DonHang>()
                .HasMany(e => e.ChiTietDonHangs)
                .WithRequired(e => e.DonHang)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<GioHang>()
                .HasMany(e => e.GioHangChiTiets)
                .WithRequired(e => e.GioHang)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<KichThuoc>()
                .HasMany(e => e.BienTheSanPhams)
                .WithRequired(e => e.KichThuoc)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<MauSac>()
                .HasMany(e => e.BienTheSanPhams)
                .WithRequired(e => e.MauSac)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<NguoiDung>()
                .HasMany(e => e.DanhGias)
                .WithRequired(e => e.NguoiDung)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<NguoiDung>()
                .HasMany(e => e.DiaChis)
                .WithRequired(e => e.NguoiDung)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<NguoiDung>()
                .HasMany(e => e.LichSuXems)
                .WithOptional(e => e.NguoiDung)
                .HasForeignKey(e => e.NguoiDungID);

            modelBuilder.Entity<NguoiDung>()
                .HasMany(e => e.SanPhamYeuThiches)
                .WithRequired(e => e.NguoiDung)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<SanPham>()
                .Property(e => e.GiaGoc)
                .HasPrecision(18, 0);

            modelBuilder.Entity<SanPham>()
                .Property(e => e.GiaBan)
                .HasPrecision(18, 0);

            modelBuilder.Entity<SanPham>()
                .HasMany(e => e.AnhSanPhams)
                .WithRequired(e => e.SanPham)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<SanPham>()
                .HasMany(e => e.BienTheSanPhams)
                .WithRequired(e => e.SanPham)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<SanPham>()
                .HasMany(e => e.DanhGias)
                .WithRequired(e => e.SanPham)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<SanPham>()
                .HasMany(e => e.GoiYSanPhams)
                .WithOptional(e => e.SanPham)
                .HasForeignKey(e => e.SanPhamNguonID);

            modelBuilder.Entity<SanPham>()
                .HasMany(e => e.GoiYSanPhams1)
                .WithRequired(e => e.SanPham1)
                .HasForeignKey(e => e.SanPhamDuocGoiYID)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<SanPham>()
                .HasMany(e => e.LichSuXems)
                .WithRequired(e => e.SanPham)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<SanPham>()
                .HasMany(e => e.SanPhamYeuThiches)
                .WithRequired(e => e.SanPham)
                .WillCascadeOnDelete(false);
        }
    }
}
