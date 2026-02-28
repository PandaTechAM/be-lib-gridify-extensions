# Pandatech.GridifyExtensions

Extensions for [Gridify](https://github.com/alirezanet/Gridify) providing streamlined filtering, ordering, pagination,
and aggregation for .NET 8+ with Entity Framework Core.

## Installation

```bash
dotnet add package Pandatech.GridifyExtensions
```

## Setup

Register Gridify with custom mapper discovery:

```csharp
builder.AddGridify(); // Scans calling assembly
// or
builder.AddGridify(typeof(Program).Assembly, typeof(Domain).Assembly);
```

## Quick Start

### 1. Define Entity Mapper

```csharp
public class Book
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public DateTime PublishedDate { get; set; }
    public decimal Price { get; set; }
}

public class BookMapper : FilterMapper<Book>
{
    public BookMapper()
    {
        GenerateMappings(); // Auto-map all properties
        
        // Custom mappings
        AddMap("book-id", x => x.Id);
        AddMap("title", x => x.Title.ToLower(), x => x.ToLower());
        AddMap("published", x => x.PublishedDate, x => x.ToUtcDateTime());
        
        // Default sort order
        AddDefaultOrderByDescending("published");
    }
}
```

### 2. Query with Filtering & Pagination

```csharp
// Paged response
var result = await dbContext.Books
    .FilterOrderAndGetPagedAsync(
        new GridifyQueryModel { 
            Page = 1, 
            PageSize = 20,
            Filter = "title=*potter*",
            OrderBy = "published desc"
        },
        cancellationToken
    );

// With projection
var dtos = await dbContext.Books
    .FilterOrderAndGetPagedAsync(
        new GridifyQueryModel { Page = 1, PageSize = 20 },
        x => new BookDto { Title = x.Title, Price = x.Price },
        cancellationToken
    );
```

### 3. Cursor-Based Pagination

```csharp
var result = await dbContext.Books
    .FilterOrderAndGetCursoredAsync(
        new GridifyCursoredQueryModel { 
            PageSize = 50,
            Filter = "price>10"
        },
        cancellationToken
    );
```

## Core Features

### Filtering & Ordering

```csharp
// Apply filter only
var filtered = query.ApplyFilter("title=*science*");

// Apply filter from model
var filtered = query.ApplyFilter(new GridifyQueryModel { Filter = "price<20" });

// Apply ordering
var ordered = query.ApplyOrder(new GridifyQueryModel { OrderBy = "title" });
```

### Pagination

```csharp
// Traditional paging
var paged = await query.GetPagedAsync(
    new GridifyQueryModel { Page = 2, PageSize = 10 },
    cancellationToken
);

// Cursor-based (more efficient for large datasets)
var cursored = await query.FilterOrderAndGetCursoredAsync(
    new GridifyCursoredQueryModel { PageSize = 50 },
    cancellationToken
);
```

### Column Distinct Values

```csharp
// Get distinct values for a column (with cursor pagination)
var distinctTitles = await dbContext.Books
    .ColumnDistinctValuesAsync(
        new ColumnDistinctValueCursoredQueryModel { 
            PropertyName = "title",
            PageSize = 50,
            Filter = "title>A"
        },
        cancellationToken
    );

// For encrypted columns, provide decryptor
var decrypted = await query.ColumnDistinctValuesAsync(
    model,
    decryptor: bytes => Decrypt(bytes),
    cancellationToken
);
```

### Aggregations

```csharp
var total = await dbContext.Books
    .AggregateAsync(
        new AggregateQueryModel { 
            AggregateType = AggregateType.Sum,
            PropertyName = "price"
        },
        cancellationToken
    );
```

**Supported aggregations:**

- `UniqueCount` - Count of distinct values
- `Sum` - Sum of values
- `Average` - Average value
- `Min` - Minimum value
- `Max` - Maximum value

## Advanced Features

### Custom Converters

```csharp
public class DeviceMapper : FilterMapper<Device>
{
    public DeviceMapper()
    {
        GenerateMappings();
        
        // Case-insensitive string matching
        AddMap("name", x => x.Name.ToLower(), x => x.ToLower());
        
        // DateTime with UTC conversion
        AddMap("created", x => x.CreatedAt, x => x.ToUtcDateTime());
        
        AddDefaultOrderByDescending("created");
    }
}
```

### Encrypted Columns

```csharp
public class UserMapper : FilterMapper<User>
{
    public UserMapper()
    {
        // Mark column as encrypted
        AddMap("email", x => x.EncryptedEmail, isEncrypted: true);
    }
}

// Query with decryption
var users = await dbContext.Users
    .ColumnDistinctValuesAsync(
        new ColumnDistinctValueCursoredQueryModel { PropertyName = "email" },
        decryptor: encryptedBytes => AesDecrypt(encryptedBytes),
        cancellationToken
    );
```

### Complex Mappings

```csharp
// Navigation properties
AddMap("author-name", x => x.Author.Name);

// Collections
AddMap("tags", x => x.Tags.Select(t => t.Name));

// Nested properties
AddMap("publisher-country", x => x.Publisher.Address.Country);
```

### Page Size Limits

By default, `GridifyQueryModel` limits `PageSize` to 500:

```csharp
// Remove limit via constructor
var model = new GridifyQueryModel(validatePageSize: false) { 
    PageSize = 1000, 
    Page = 1 
};

// Or set to maximum
model.SetMaxPageSize(); // Sets PageSize to int.MaxValue
```

### Custom Flag Operator

Built-in support for bitwise flag checking:

```csharp
// Filter by enum flags
var query = "permissions#hasFlag16"; // checks if bit 16 is set
```

## Gridify Query Syntax

Standard Gridify filtering syntax is supported:

```csharp
// Operators: =, !=, <, >, <=, >=, =*, *=, =*=
Filter = "title=*potter*, price<20, published>2020-01-01"

// Logical: , (AND), | (OR), () (grouping)
Filter = "(title=*fantasy*|title=*scifi*), price<30"

// Custom operators
Filter = "flags#hasFlag4" // bitwise flag check
```

## API Reference

### Extension Methods

| Method                                  | Description                |
|-----------------------------------------|----------------------------|
| `ApplyFilter(model/filter)`             | Apply filtering            |
| `ApplyOrder(model)`                     | Apply ordering             |
| `GetPagedAsync(model)`                  | Get paged results          |
| `FilterOrderAndGetPagedAsync(model)`    | Filter + order + page      |
| `FilterOrderAndGetCursoredAsync(model)` | Cursor-based pagination    |
| `ColumnDistinctValuesAsync(model)`      | Get distinct column values |
| `AggregateAsync(model)`                 | Perform aggregation        |

### Model Types

- `GridifyQueryModel` - Traditional pagination with page number
- `GridifyCursoredQueryModel` - Cursor-based pagination
- `ColumnDistinctValueCursoredQueryModel` - Distinct values with cursor
- `AggregateQueryModel` - Aggregation configuration

### Response Types

- `PagedResponse<T>` - `(Data, Page, PageSize, TotalCount)`
- `CursoredResponse<T>` - `(Data, PageSize)`

## License

MIT