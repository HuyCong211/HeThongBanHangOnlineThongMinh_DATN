using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using DoAn_Ver2.Models;

namespace DoAn_Ver2.Infrastructure
{
    public class UnitOfWork : IDisposable
    {
        private DoAn_DbModel _context = new DoAn_DbModel();

        // Khai báo các Repository cụ thể ở đây nếu cần mở rộng sau này
        private Repository<SanPham> _sanPhamRepository;
        private Repository<DanhMuc> _danhMucRepository;

        // Generic Repository getter (Dùng cái này cho nhanh với các bảng đơn giản)
        public Repository<T> Repository<T>() where T : class
        {
            return new Repository<T>(_context);
        }

        public Repository<SanPham> SanPhamRepository
        {
            get
            {
                if (_sanPhamRepository == null)
                    _sanPhamRepository = new Repository<SanPham>(_context);
                return _sanPhamRepository;
            }
        }

        public void Save()
        {
            _context.SaveChanges();
        }

        private bool disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    _context.Dispose();
                }
            }
            this.disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}