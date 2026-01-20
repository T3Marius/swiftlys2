    private static nint? _$NAME$Offset;

    public string $NAME$ {
        get {
            _$NAME$Offset = _$NAME$Offset ?? Schema.GetOffset($HASH$);
            return Schema.GetCUtlString(_Handle.Read<nint>(_$NAME$Offset!.Value));
        }
        set {
            _$NAME$Offset = _$NAME$Offset ?? Schema.GetOffset($HASH$);
            Schema.SetCUtlString(_Handle, _$NAME$Offset!.Value, value);
        }
    } 