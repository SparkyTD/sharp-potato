namespace sharp_potato;

public class Range<T>
{
    public int Length { get; }

    private readonly T[] backingArray;
    private readonly int startIndex;

    public Range(T[] backingArray) : this(backingArray, 0, backingArray.Length)
    {
    }

    public Range(T[] backingArray, int startIndex) : this(backingArray, startIndex, backingArray.Length - startIndex)
    {
    }

    public Range(T[] backingArray, int startIndex, int length)
    {
        this.backingArray = backingArray;
        this.startIndex = startIndex;
        Length = length;
    }

    public T[] ToArray() => backingArray[startIndex..(startIndex + Length)];

    public T this[int index]
    {
        get => backingArray[startIndex + index];
        set => backingArray[startIndex + index] = value;
    }

    public Range<T> SubRange(int start) => new(backingArray, startIndex + start, backingArray.Length - (startIndex + start));
    public Range<T> SubRange(int start, int length) => new(backingArray, startIndex + start, length);
}

public static class RangeEx
{
    public static Range<T> Range<T>(this T[] array) => new(array);
    public static Range<T> Range<T>(this T[] array, int startIndex) => new(array, startIndex);
    public static Range<T> Range<T>(this T[] array, int startIndex, int length) => new(array, startIndex, length);
}