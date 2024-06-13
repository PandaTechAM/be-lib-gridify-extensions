using Gridify;
using GridifyExtensions.Exceptions;

namespace GridifyExtensions.Models
{
    public class GridifyQueryModel : GridifyQuery
    {
        public new int Page
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

        public new int PageSize
        {
            get => base.PageSize;
            set
            {
                if (value <= 0)
                {
                    throw new GridifyException($"{nameof(PageSize)} should be positive number.");
                }

                if (value > 500)
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
    }
}
