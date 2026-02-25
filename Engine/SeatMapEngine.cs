using System.Runtime.CompilerServices;

namespace Hive_Movie.Engine;

/// <summary>
/// High-performance, zero-allocation seat state engine.
/// 
/// This type provides in-memory seat state management over a raw byte array
/// representing a cinema room in row-major order:
/// 
///     index = (row * columns) + column
/// 
/// DESIGN GOALS:
/// • Zero allocations during seat operations
/// • O(1) single-seat access
/// • O(n) group reservation
/// • Cache-friendly contiguous memory layout
/// 
/// MEMORY MODEL:
/// The underlying <see cref="byte"/> array is externally owned and heap-allocated.
/// This engine does not allocate, copy, or resize the buffer.
/// All operations mutate the provided array directly.
/// 
/// CONCURRENCY:
/// This type is NOT thread-safe.
/// It is intended to be used within a single request scope.
/// 
/// Cross-request and cross-node concurrency must be handled by the
/// persistence layer (e.g., database RowVersion optimistic concurrency token).
/// 
/// PERFORMANCE CHARACTERISTICS:
/// • GetStatus: O(1), no allocations
/// • TryReserveSeat: O(1), no allocations
/// • TryReserveSeats: O(n), no allocations
/// • MarkAsSold: O(1)
/// • ReleaseSeat: O(1)
/// 
/// STATE TRANSITIONS:
/// • Available → Reserved
/// • Reserved → Sold
/// • Reserved → Available
/// Invalid transitions throw <see cref="InvalidOperationException"/>.
/// 
/// VALIDATION:
/// All seat coordinate access is bounds-checked.
/// The constructor validates:
///     state.Length == rows * maxColumns.
/// 
/// This struct is declared readonly to prevent accidental reassignment
/// of its fields. The underlying array remains mutable.
/// </summary>
public readonly struct SeatMapEngine
{
    private readonly byte[] _state;
    private readonly int _maxColumns;
    private readonly int _maxRows;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeatMapEngine"/>.
    /// </summary>
    /// <param name="state">
    /// The raw byte array representing seat states.
    /// Each element must correspond to a valid <see cref="SeatStatus"/> value.
    /// </param>
    /// <param name="rows">Total number of rows in the room.</param>
    /// <param name="maxColumns">Total number of columns (seats per row).</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="state"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="rows"/> or <paramref name="maxColumns"/> is less than or equal to zero.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="state"/> length does not equal rows * maxColumns.
    /// </exception>
    public SeatMapEngine(byte[] state, int rows, int maxColumns)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));

        if (rows <= 0)
            throw new ArgumentOutOfRangeException(nameof(rows));

        if (maxColumns <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxColumns));

        if (state.Length != rows * maxColumns)
            throw new ArgumentException("State array length must equal rows * maxColumns.");

        _maxRows = rows;
        _maxColumns = maxColumns;
    }

    /// <summary>
    /// Converts a two-dimensional seat coordinate into a one-dimensional array index.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="col">Zero-based column index.</param>
    /// <returns>The calculated index within the underlying state array.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if the specified row or column is outside room bounds.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetIndex(int row, int col)
    {
        if (row < 0 || row >= _maxRows)
            throw new ArgumentOutOfRangeException(nameof(row));

        if (col < 0 || col >= _maxColumns)
            throw new ArgumentOutOfRangeException(nameof(col));

        return row * _maxColumns + col;
    }

    /// <summary>
    /// Gets the current status of a specific seat.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="col">Zero-based column index.</param>
    /// <returns>The current <see cref="SeatStatus"/> of the seat.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if the specified coordinates are outside room bounds.
    /// </exception>
    public SeatStatus GetStatus(int row, int col)
    {
        return (SeatStatus)_state[GetIndex(row, col)];
    }

    /// <summary>
    /// Attempts to reserve a single seat.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="col">Zero-based column index.</param>
    /// <returns>
    /// True if the seat was available and is now reserved;
    /// otherwise, false.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if the specified coordinates are outside room bounds.
    /// </exception>
    public bool TryReserveSeat(int row, int col)
    {
        var index = GetIndex(row, col);

        if (_state[index] != (byte)SeatStatus.Available)
            return false;

        _state[index] = (byte)SeatStatus.Reserved;
        return true;
    }

    /// <summary>
    /// Attempts to reserve a group of seats atomically (in-memory).
    /// 
    /// Either all seats are reserved, or none are.
    /// This method performs two passes over the collection to avoid allocations.
    /// </summary>
    /// <param name="seatsToBook">
    /// A read-only collection of seat coordinates represented as (Row, Col).
    /// </param>
    /// <returns>
    /// True if every seat is available and successfully reserved;
    /// otherwise, false.
    /// </returns>
    /// <remarks>
    /// This method performs:
    /// 1. Verification phase (read-only)
    /// 2. Reservation phase (write)
    /// 
    /// No intermediate allocations occur.
    /// </remarks>
    public bool TryReserveSeats(IReadOnlyCollection<(int Row, int Col)>? seatsToBook)
    {
        if (seatsToBook is null || seatsToBook.Count == 0)
            return false;

        // Phase 1: Verification
        foreach (var seat in seatsToBook)
        {
            if (_state[GetIndex(seat.Row, seat.Col)] != (byte)SeatStatus.Available)
            {
                return false; 
            }
        }

        // Phase 2: Reservation
        foreach (var seat in seatsToBook)
        {
            _state[GetIndex(seat.Row, seat.Col)] = (byte)SeatStatus.Reserved;
        }

        return true;
    }

    /// <summary>
    /// Marks a reserved seat as sold.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="col">Zero-based column index.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if the specified coordinates are outside room bounds.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the seat is not currently reserved.
    /// </exception>
    public void MarkAsSold(int row, int col)
    {
        var index = GetIndex(row, col);

        if (_state[index] != (byte)SeatStatus.Reserved)
            throw new InvalidOperationException("Only reserved seats can be marked as sold.");

        _state[index] = (byte)SeatStatus.Sold;
    }

    /// <summary>
    /// Releases a reserved seat and marks it as available.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="col">Zero-based column index.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if the specified coordinates are outside room bounds.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the seat is not currently reserved.
    /// </exception>
    public void ReleaseSeat(int row, int col)
    {
        var index = GetIndex(row, col);

        if (_state[index] != (byte)SeatStatus.Reserved)
            throw new InvalidOperationException("Only reserved seats can be released.");

        _state[index] = (byte)SeatStatus.Available;
    }
}