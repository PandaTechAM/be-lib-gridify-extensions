# Pandatech.GridifyExtensions

Welcome to **Pandatech.GridifyExtensions**! This library extends the
powerful [Gridify](https://github.com/alirezanet/Gridify) package, providing additional functionalities and a more
streamlined API for data filtering, ordering, and pagination in .NET applications.

## Why Choose Pandatech.GridifyExtensions?

Gridify is great for dynamic querying, but incorporating it into projects can sometimes be repetitive or involve extra
setup. **Pandatech.GridifyExtensions** makes this process more efficient by:

- **Extending Functionality:** Additional methods to handle common data filtering, ordering, and pagination scenarios.
- **Simplifying the API:** Reducing boilerplate code, making your code cleaner and easier to maintain.
- **Improving Integration:** Seamlessly integrates with .NET and EF Core projects, reducing the overhead of adding
  dynamic querying to your applications.

## Features

- **Dynamic Filtering & Ordering:** Easily apply complex filters and ordering to your queries using simple methods.
- **Pagination & Cursor Support:** Paginate data efficiently with support for both traditional pagination and
  cursor-based pagination for better scalability.
- **Custom Mappings:** Create custom property mappings for your entities to support advanced querying.
- **Support for Encrypted Fields:** Automatically decrypt values with the provided decryptor function.
- **Aggregation Support:** Perform common aggregate operations like sum, average, min, and max.

## Installation

Install the package via NuGet:

```bash
dotnet add package Pandatech.Gridify.Extensions
```

## Setup

To enable Gridify support and register custom mapping classes, call the `AddGridify` method on the
`WebApplicationBuilder`.

```csharp
builder.AddGridify(params Assembly[] assemblies);
```

You can specify which assemblies to search for configurations. If no assemblies are provided, the current assembly will
be used.

## Usage

### Creating Mappings for Your Entities:

To efficiently filter and query your Book entity using Gridify, you need to create a mapping class that extends
`FilterMapper<T>.` This class will define how each property in your entity should be mapped for filtering.

Here’s an example of how to set up the Book entity and its corresponding mapping class:

```csharp
public class Book
{
    public Guid BookId { get; set; } = Guid.NewGuid();
    public string Title { get; set; }
    public DateTime Date { get; set; }
    public int Count { get; set; }
    public long CountLong { get; set; }
    public decimal CountDecimal { get; set; }
    public ICollection<Book> Books { get; set; }
    public Book OtherBook { get; set; }
}

public class BookMapper : FilterMapper<Book>
{
    public BookMapper()
    {
        GenerateMappings();
        // Map "book-id" to BookId property
        AddMap("book-id", x => x.BookId);

        // Map "count" to Count property with an optional converter
        AddMap("count", x => x.Count, x => x.ToLower());

        // Map "count-long" to CountLong property
        AddMap("count-long", x => x.CountLong);

        // Map "count-decimal" to CountDecimal property
        AddMap("count-decimal", x => x.CountDecimal);

        // Map "other-dates" to the Date property of the Books collection
        AddMap("other-dates", x => x.Books.Select(b => b.Date));

        // Map "other-book-id" to the BookId property of the OtherBook property
        AddMap("other-book-id", x => x.OtherBook.BookId);
        
      AddDefaultOrderByDescending("book-id");        
    }
}
```

### Adding Converters

You can specify a converter function as the third parameter in the AddMap method to transform the value before it is
used.
This is useful for custom data manipulation and formatting.

```csharp
public class DeviceFilters : FilterMapper<Device>
{
   public DeviceFilters()
   {
      GenerateMappings();
      AddMap("Name", x => x.Name.ToLower(), x => x.ToLower());
      AddMap("OsType", x => x.OsType.ToLower(), x => x.ToLower());
      AddMap("OsVersion", x => x.OsVersion.ToLower(), x => x.ToLower());
      AddMap("BrowserType", x => x.BrowserType.ToLower(), x => x.ToLower());
      AddMap("BrowserVersion", x => x.BrowserVersion.ToLower(), x => x.ToLower());
      AddMap("UniqueIdPerDevice", x => x.UniqueIdPerDevice.ToLower(), x => x.ToLower());
      AddMap("CreatedAt", x => x.CreatedAt, x => x.ToUtcDateTime()); //This is must for date time fields
      AddMap("UpdatedAt", x => x.UpdatedAt, x => x.ToUtcDateTime()); //This is must for date time fields

      AddDefaultOrderByDescending("Id");
   }
}
```

### Filtering, Sorting, and Paging

Use the `FilterOrderAndGetPagedAsync` method to apply filtering, sorting, and paging to your queries:

```csharp
var pagedResponse = await dbContext.Books
    .FilterOrderAndGetPagedAsync(new GridifyQueryModel { PageSize = 10, Page = 1 }, cancellationToken);
```

Use the `FilterOrderAndGetPagedAsync` method to apply filtering, sorting, and paging to your queries with selected
columns:

```csharp
var pagedBooks = await dbContext.Books
    .FilterOrderAndGetPagedAsync(new GridifyQueryModel { Page = 1, PageSize = 10 }, x => new BookDto { Title = x.Title }, cancellationToken);
```

```csharp

**Gridify QueryModel**

By default, `GridifyQueryModel` limits `PageSize` to 500 records. To remove this restriction, initialize it with
`false`:

```csharp
var gridifyQueryModel = new GridifyQueryModel(false) { PageSize = 10, Page = 1 };
```

Alternatively, you can set the `PageSize` to the maximum value with:

```csharp
gridifyQueryModel.SetMaxPageSize();
```

### Cursor-Based Pagination

Use the `FilterOrderAndGetCursoredAsync` method for efficient, scalable cursor-based pagination:

```csharp
var cursoredResponse = await dbContext.Books
.FilterOrderAndGetCursoredAsync(new GridifyCursoredQueryModel { PageSize = 50, Filter="Title>abc"}, cancellationToken);
```

Use the `FilterOrderAndGetCursoredAsync` method for efficient, scalable cursor-based pagination with selected columns:

```csharp
var cursoredBooks = await dbContext.Books
    .FilterOrderAndGetCursoredAsync(new GridifyCursoredQueryModel { PageSize = 50, Filter="Title>abc" }, x => new BookDto { Title = x.Title }, cancellationToken);
```

### Distinct Values with Cursors

Get distinct values of a specific column using cursor-based pagination:

```csharp
var distinctValues = await dbContext.Books
    .ColumnDistinctValuesAsync(new ColumnDistinctValueCursoredQueryModel { PropertyName = "Title", PageSize = 50, Filter="Title>abc" }, cancellationToken);
```

### Aggregation Operations

Perform aggregate operations like sum, average, count, min, and max using `AggregateAsync`:

```csharp
var aggregateResult = await dbContext.Books
    .AggregateAsync(new AggregateQueryModel { AggregateType = AggregateType.Sum, PropertyName = "Count" }, cancellationToken);
```

## License

Pandatech.GridifyExtensions is licensed under the MIT License.
