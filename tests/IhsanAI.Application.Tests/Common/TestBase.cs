using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;
using System.Linq.Expressions;

namespace IhsanAI.Application.Tests.Common;

/// <summary>
/// Base class for all unit tests providing common mock setups
/// </summary>
public abstract class TestBase
{
    protected Mock<IApplicationDbContext> DbContextMock { get; }
    protected Mock<IDateTimeService> DateTimeServiceMock { get; }
    protected Mock<ICurrentUserService> CurrentUserServiceMock { get; }
    protected DateTime TestDateTime { get; }

    protected TestBase()
    {
        DbContextMock = new Mock<IApplicationDbContext>();
        DateTimeServiceMock = new Mock<IDateTimeService>();
        CurrentUserServiceMock = new Mock<ICurrentUserService>();
        TestDateTime = new DateTime(2024, 6, 15, 10, 30, 0);

        // Setup default date time
        DateTimeServiceMock.Setup(x => x.Now).Returns(TestDateTime);
    }

    /// <summary>
    /// Creates a mock DbSet from a list of entities
    /// </summary>
    protected static Mock<DbSet<T>> CreateMockDbSet<T>(List<T> data) where T : class
    {
        var queryable = data.AsQueryable();
        var mockSet = new Mock<DbSet<T>>();

        mockSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));

        mockSet.As<IQueryable<T>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<T>(queryable.Provider));

        mockSet.As<IQueryable<T>>()
            .Setup(m => m.Expression)
            .Returns(queryable.Expression);

        mockSet.As<IQueryable<T>>()
            .Setup(m => m.ElementType)
            .Returns(queryable.ElementType);

        mockSet.As<IQueryable<T>>()
            .Setup(m => m.GetEnumerator())
            .Returns(() => queryable.GetEnumerator());

        return mockSet;
    }

    /// <summary>
    /// Creates a test user with common properties
    /// </summary>
    protected static Kullanici CreateTestUser(
        int id = 1,
        string email = "test@ihsanai.com",
        string password = "test123",
        string name = "Test",
        string surname = "User",
        int? firmaId = 1,
        int? subeId = 1,
        int? muhasebeYetkiId = 1,
        sbyte onay = 1)
    {
        return new Kullanici
        {
            Id = id,
            Email = email,
            Parola = password,
            Adi = name,
            Soyadi = surname,
            FirmaId = firmaId,
            SubeId = subeId,
            MuhasebeYetkiId = muhasebeYetkiId,
            Onay = onay,
            KayitTarihi = DateTime.Now.AddDays(-30),
            BitisTarihi = null
        };
    }

    /// <summary>
    /// Creates a test permission (Yetki) entity
    /// </summary>
    protected static Yetki CreateTestYetki(
        int id = 1,
        string yetkiAdi = "Admin",
        string gorebilecegiPoliceler = "1")
    {
        return new Yetki
        {
            Id = id,
            YetkiAdi = yetkiAdi,
            FirmaId = 1,
            EkleyenUyeId = 1,
            GorebilecegiPolicelerveKartlar = gorebilecegiPoliceler,
            PoliceDuzenleyebilsin = "1",
            PoliceHavuzunuGorebilsin = "1",
            PoliceAktarabilsin = "1",
            PoliceDosyalarinaErisebilsin = "1",
            PoliceYakalamaSecenekleri = "1",
            YetkilerSayfasindaIslemYapabilsin = "1",
            AcenteliklerSayfasindaIslemYapabilsin = "1",
            KomisyonOranlariniDuzenleyebilsin = "1",
            ProduktorleriGorebilsin = "1",
            AcenteliklereGorePoliceYakalansin = "1",
            MusterileriGorebilsin = "1",
            MusteriListesiGorebilsin = "1",
            MusteriDetayGorebilsin = "1",
            YenilemeTakibiGorebilsin = "1",
            FinansSayfasiniGorebilsin = "1",
            FinansDashboardGorebilsin = "1",
            PoliceOdemeleriGorebilsin = "1",
            TahsilatTakibiGorebilsin = "1",
            FinansRaporlariGorebilsin = "1",
            KazanclarimGorebilsin = "1",
            DriveEntegrasyonuGorebilsin = "1",
            KayitTarihi = DateTime.Now.AddDays(-60)
        };
    }

    /// <summary>
    /// Creates a test branch (Sube) entity
    /// </summary>
    protected static Sube CreateTestSube(int id = 1, string subeAdi = "Test Sube")
    {
        return new Sube
        {
            Id = id,
            SubeAdi = subeAdi
        };
    }
}

/// <summary>
/// Async query provider for EF Core mock testing
/// </summary>
internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    internal TestAsyncQueryProvider(IQueryProvider inner)
    {
        _inner = inner;
    }

    public IQueryable CreateQuery(Expression expression)
    {
        return new TestAsyncEnumerable<TEntity>(expression);
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new TestAsyncEnumerable<TElement>(expression);
    }

    public object? Execute(Expression expression)
    {
        return _inner.Execute(expression);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return _inner.Execute<TResult>(expression);
    }

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = typeof(IQueryProvider)
            .GetMethod(
                name: nameof(IQueryProvider.Execute),
                genericParameterCount: 1,
                types: new[] { typeof(Expression) })
            ?.MakeGenericMethod(resultType)
            .Invoke(this, new[] { expression });

        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))
            ?.MakeGenericMethod(resultType)
            .Invoke(null, new[] { executionResult })!;
    }
}

internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable)
        : base(enumerable)
    { }

    public TestAsyncEnumerable(Expression expression)
        : base(expression)
    { }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner)
    {
        _inner = inner;
    }

    public T Current => _inner.Current;

    public ValueTask<bool> MoveNextAsync()
    {
        return ValueTask.FromResult(_inner.MoveNext());
    }

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return ValueTask.CompletedTask;
    }
}
