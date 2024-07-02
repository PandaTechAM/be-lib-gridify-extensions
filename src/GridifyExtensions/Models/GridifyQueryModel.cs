using Gridify;
using GridifyExtensions.Exceptions;

namespace GridifyExtensions.Models
{
    public class GridifyQueryModel : GridifyQuery
    {
        private bool _validatePageSize;

        public GridifyQueryModel()
        {
            _validatePageSize = true;
        }

        public GridifyQueryModel(bool validatePageSize)
        {
            _validatePageSize = validatePageSize;
        }

        public new required int Page
        {
            get => base.Page;
            set
            {
                if (value <= 0)
                {
                    throw new GridifyException($"{nameof(Page)} should be positive number.");
                }

                base.Page = value;
            }
        }

        public new required int PageSize
        {
            get => base.PageSize;
            set
            {
                if (value <= 0)
                {
                    throw new GridifyException($"{nameof(PageSize)} should be positive number.");
                }

                if (value > 500 && _validatePageSize)
                {
                    value = 500;
                }

                base.PageSize = value;
            }
        }

        public new string? OrderBy
        {
            get => base.OrderBy;
            set => base.OrderBy = value;
        }

        public new string? Filter
        {
            get => base.Filter;
            set => base.Filter = value;
        }

        public void SetMaxPageSize()
        {
            _validatePageSize = false;
            PageSize = int.MaxValue;
        }
    }
}