  private static nint? _$NAME$Offset;

  public string $NAME$ {
    get {
        if (_$NAME$Offset == null) {
            _$NAME$Offset = Schema.GetOffset($HASH$);
        }
        var ptr = _Handle + _$NAME$Offset!.Value;
        return Schema.GetString(ptr);
    }
    set {
        if (_$NAME$Offset == null) {
            _$NAME$Offset = Schema.GetOffset($HASH$);
        }
        Schema.SetFixedString(_Handle, _$NAME$Offset!.Value, value, $ELEMENT_COUNT$);
    }
  } 