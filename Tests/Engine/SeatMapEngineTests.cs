using Hive_Movie.Engine;
namespace Tests.Engine;

public class SeatMapEngineTests
{
    private static SeatMapEngine CreateEngine(out byte[] state, int rows = 10, int cols = 10)
    {
        state = new byte[rows * cols];
        return new SeatMapEngine(state, rows, cols);
    }

    // =========================================================================
    // 1. CONSTRUCTOR
    // =========================================================================

    [Fact]
    public void Constructor_ValidInput_InitializesCorrectly()
    {
        var engine = CreateEngine(out _);
        Assert.Equal(SeatStatus.Available, engine.GetStatus(0, 0));
    }

    [Fact]
    public void Constructor_1x1Grid_Works()
    {
        // 👉 FIX: Updated call order
        var engine = CreateEngine(out _, 1, 1);
        Assert.Equal(SeatStatus.Available, engine.GetStatus(0, 0));
    }

    [Fact]
    public void Constructor_NullState_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SeatMapEngine(null!, 10, 10));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    public void Constructor_InvalidDimensions_Throws(int rows, int cols)
    {
        var state = new byte[100];
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SeatMapEngine(state, rows, cols));
    }

    [Fact]
    public void Constructor_LengthMismatch_Throws()
    {
        var state = new byte[99];
        Assert.Throws<ArgumentException>(() =>
            new SeatMapEngine(state, 10, 10));
    }

    // =========================================================================
    // 2. BOUNDS & MEMORY MAPPING
    // =========================================================================

    [Fact]
    public void GetStatus_MapsCorrectly_ToUnderlyingArray()
    {
        var state = new byte[12];
        state[0] = (byte)SeatStatus.Sold;
        state[3] = (byte)SeatStatus.Reserved;
        state[8] = (byte)SeatStatus.Reserved;
        state[11] = (byte)SeatStatus.Sold;

        var engine = new SeatMapEngine(state, 3, 4);

        Assert.Equal(SeatStatus.Sold, engine.GetStatus(0, 0));
        Assert.Equal(SeatStatus.Reserved, engine.GetStatus(0, 3));
        Assert.Equal(SeatStatus.Reserved, engine.GetStatus(2, 0));
        Assert.Equal(SeatStatus.Sold, engine.GetStatus(2, 3));
        Assert.Equal(SeatStatus.Available, engine.GetStatus(1, 1));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(10, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 10)]
    public void AllCoordinateMethods_OutOfBounds_Throw(int row, int col)
    {
        var engine = CreateEngine(out _);

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.GetStatus(row, col));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.TryReserveSeat(row, col));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.MarkAsSold(row, col));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.ReleaseSeat(row, col));
    }

    // =========================================================================
    // 3. SINGLE SEAT
    // =========================================================================

    [Fact]
    public void TryReserveSeat_Available_Reserves()
    {
        var engine = CreateEngine(out _);

        Assert.True(engine.TryReserveSeat(5, 5));
        Assert.Equal(SeatStatus.Reserved, engine.GetStatus(5, 5));
    }

    [Theory]
    [InlineData(SeatStatus.Reserved)]
    [InlineData(SeatStatus.Sold)]
    public void TryReserveSeat_NotAvailable_ReturnsFalse(SeatStatus initial)
    {
        var engine = CreateEngine(out var state);
        state[55] = (byte)initial;

        Assert.False(engine.TryReserveSeat(5, 5));
        Assert.Equal(initial, engine.GetStatus(5, 5));
    }

    // =========================================================================
    // 4. BATCH RESERVATION
    // =========================================================================

    [Fact]
    public void TryReserveSeats_NullOrEmpty_ReturnsFalse()
    {
        var engine = CreateEngine(out _);

        Assert.False(engine.TryReserveSeats(null));
        Assert.False(engine.TryReserveSeats(new List<(int, int)>()));
    }

    [Fact]
    public void TryReserveSeats_AllAvailable_ReservesAll()
    {
        var engine = CreateEngine(out _);
        var seats = new List<(int, int)>
        {
            (0, 0), (4, 4), (9, 9)
        };

        Assert.True(engine.TryReserveSeats(seats));

        foreach (var (r, c) in seats)
        {
            Assert.Equal(SeatStatus.Reserved, engine.GetStatus(r, c));
        }
    }

    [Fact]
    public void TryReserveSeats_WhenAnyUnavailable_IsAtomic()
    {
        var engine = CreateEngine(out var state);
        state[44] = (byte)SeatStatus.Sold;

        var seats = new List<(int, int)>
        {
            (0, 0), (4, 4), (9, 9)
        };

        Assert.False(engine.TryReserveSeats(seats));

        Assert.Equal(SeatStatus.Available, engine.GetStatus(0, 0));
        Assert.Equal(SeatStatus.Sold, engine.GetStatus(4, 4));
        Assert.Equal(SeatStatus.Available, engine.GetStatus(9, 9));
    }

    [Fact]
    public void TryReserveSeats_DuplicateCoordinates_RemainsStable()
    {
        var engine = CreateEngine(out _);
        var seats = new List<(int, int)>
        {
            (0, 0), (0, 0)
        };

        Assert.True(engine.TryReserveSeats(seats));
        Assert.Equal(SeatStatus.Reserved, engine.GetStatus(0, 0));
    }

    // Ensures boundary check failure rolls back phase 1
    [Fact]
    public void TryReserveSeats_OutOfBoundsSeat_ThrowsAndRollsBack()
    {
        var engine = CreateEngine(out _);

        // (0,0) is valid, but (99,99) will crash the boundary check
        var seats = new List<(int, int)>
        {
            (0, 0), (99, 99)
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.TryReserveSeats(seats));

        // CRITICAL: Prove the crash prevented (0,0) from being locked!
        Assert.Equal(SeatStatus.Available, engine.GetStatus(0, 0));
    }

    // =========================================================================
    // 5. STATE TRANSITIONS
    // =========================================================================

    [Fact]
    public void FullLifecycle_Works()
    {
        // Updated call order
        var engine = CreateEngine(out _, 1, 1);

        Assert.Equal(SeatStatus.Available, engine.GetStatus(0, 0));

        engine.TryReserveSeat(0, 0);
        Assert.Equal(SeatStatus.Reserved, engine.GetStatus(0, 0));

        engine.MarkAsSold(0, 0);
        Assert.Equal(SeatStatus.Sold, engine.GetStatus(0, 0));

        Assert.Throws<InvalidOperationException>(() =>
            engine.ReleaseSeat(0, 0));
    }

    [Theory]
    [InlineData(SeatStatus.Available)]
    [InlineData(SeatStatus.Sold)]
    public void MarkAsSold_InvalidState_Throws(SeatStatus initial)
    {
        var engine = CreateEngine(out var state);
        state[0] = (byte)initial;

        Assert.Throws<InvalidOperationException>(() =>
            engine.MarkAsSold(0, 0));

        Assert.Equal(initial, engine.GetStatus(0, 0));
    }

    [Theory]
    [InlineData(SeatStatus.Available)]
    [InlineData(SeatStatus.Sold)]
    public void ReleaseSeat_InvalidState_Throws(SeatStatus initial)
    {
        var engine = CreateEngine(out var state);
        state[0] = (byte)initial;

        Assert.Throws<InvalidOperationException>(() =>
            engine.ReleaseSeat(0, 0));

        Assert.Equal(initial, engine.GetStatus(0, 0));
    }
}