# Pandatech.GridifyExtensions

Welcome to Pandatech.GridifyExtensions! This library builds on top of the popular Gridify package, adding new
functionalities and simplifying its use for data filtering and pagination in .NET applications.

## Why Use Pandatech.GridifyExtensions?

Gridify is a powerful tool for querying and filtering data. However, integrating it into your projects can sometimes
involve repetitive code and extra setup. Pandatech.GridifyExtensions aims to make your development process smoother by
offering:

- **Extended Functionality:** Additional methods to handle common data filtering scenarios.
- **Simplified API:** Streamlined usage to minimize boilerplate code and enhance readability.
- **Better Integration:** More intuitive integration with your existing .NET applications.

## Features
- **Dynamic Filtering:** Easily apply dynamic filtering to your queries.
- **Dynamic Ordering:** Easily apply dynamic ordering to your queries.
- **Pagination Support:** Simplified methods for paginating your data.
- **Custom Configurations:** Extend and customize Gridify configurations effortlessly.
- **Global Injection:** Inject configurations globally for consistency across your application.
- **Support for Various Data Types:** Handle multiple data types seamlessly.
## Getting Started
To get started, install the package via NuGet:

```bash
dotnet add package Pandatech.Gridify.Extensions
```

To enable Gridify support and register custom mapping classes, call the AddGridify method on the WebApplicationBuilder. 
You can specify which assemblies to search for configurations.
```csharp
builder.AddGridify(params Assembly[] assemblies);
```

## Usage
**Creating Mappings for Your Entities:**
To efficiently filter and query your Book entity using Gridify, you need to create a mapping class that extends FilterMapper<T>.
This class will define how each property in your Book entity should be mapped.

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
    }
}

```

Adding Converters

You can specify a converter function as the third parameter in the AddMap method to transform the value before it is used. 
This is useful for custom data manipulation and formatting.

**Using Extension Methods**
With Pandatech.GridifyExtensions, you can use several extension methods for filtering, sorting, and paging defined on the IQueryable<T> interface. Here are some examples:
Filtering, Sorting, and Paging

Use FilterOrderAndGetPagedAsync to apply filtering, sorting, and paging to your queries:
```csharp
public static async Task<PagedResponse<TDto>> FilterOrderAndGetPagedAsync<TEntity, TDto>(
    this IQueryable<TEntity> query, GridifyQueryModel model,
    Expression<Func<TEntity, TDto>> selectExpression, CancellationToken cancellationToken = default)

public static Task<PagedResponse<TEntity>> FilterOrderAndGetPagedAsync<TEntity>(
    this IQueryable<TEntity> query, GridifyQueryModel model, CancellationToken cancellationToken = default)
```

Example Usage:
```csharp
var pagedResponse = await dbContext.Books
    .FilterOrderAndGetPagedAsync(new GridifyQueryModel { PageSize = 10, Page = 1 }, cancellationToken);
```

Use ColumnDistinctValuesAsync to get distinct values of a specific column:
```csharp
public static async Task<PagedResponse<object>> ColumnDistinctValuesAsync<TEntity>(
    this IQueryable<TEntity> query, ColumnDistinctValueQueryModel model,
    Func<byte[], string>? decryptor = default, CancellationToken cancellationToken = default)

```
Example Usage:
```csharp
var distinctValues = await dbContext.Books
    .ColumnDistinctValuesAsync(new ColumnDistinctValueQueryModel { PropertyName = "Title" }, cancellationToken);
```

Use AggregateAsync to perform aggregation operations on your data:
```csharp
public static async Task<object> AggregateAsync<TEntity>(
    this IQueryable<TEntity> query, AggregateQueryModel model, CancellationToken cancellationToken = default)
```
Example Usage:
```csharp
var aggregateResult = await dbContext.Books
    .AggregateAsync(new AggregateQueryModel { AggregateType = AggregateType.Sum, PropertyName = "count" }, cancellationToken);
```

## License

Pandatech.GridifyExtensions is licensed under the MIT License.
