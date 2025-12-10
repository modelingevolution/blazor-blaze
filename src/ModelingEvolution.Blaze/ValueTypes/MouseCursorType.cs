namespace ModelingEvolution.Blaze;

public readonly record struct MouseCursorType(string Cursor)
{
    public static readonly MouseCursorType Default = new("default");
    public static readonly MouseCursorType Pointer = new("pointer");
    public static readonly MouseCursorType Text = new("text");
    public static readonly MouseCursorType Crosshair = new("crosshair");
    public static readonly MouseCursorType Move = new("move");
    public static readonly MouseCursorType Wait = new("wait");
    public static readonly MouseCursorType Help = new("help");
    public static readonly MouseCursorType NotAllowed = new("not-allowed");
    public static readonly MouseCursorType ZoomIn = new("zoom-in");
    public static readonly MouseCursorType ZoomOut = new("zoom-out");
    public static readonly MouseCursorType Grab = new("grab");
    public static readonly MouseCursorType Grabbing = new("grabbing");
    public static readonly MouseCursorType ResizeHorizontal = new("ew-resize");
    public static readonly MouseCursorType ResizeVertical = new("ns-resize");
    public static readonly MouseCursorType ResizeDiagonal1 = new("nwse-resize");
    public static readonly MouseCursorType ResizeDiagonal2 = new("nesw-resize");
    public static readonly MouseCursorType ColumnResize = new("col-resize");
    public static readonly MouseCursorType RowResize = new("row-resize");
    public static readonly MouseCursorType NoDrop = new("no-drop");
    public static readonly MouseCursorType Copy = new("copy");
    public static readonly MouseCursorType Alias = new("alias");
    public static readonly MouseCursorType Progress = new("progress");
    public static readonly MouseCursorType Cell = new("cell");
    public static readonly MouseCursorType ContextMenu = new("context-menu");
    public static implicit operator MouseCursorType(string cursor) => new(cursor);
    public override string ToString()
    {
        return this.Cursor;
    }
}