  private static nint? _$NAME$Offset;

  public ref $IMPL_TYPE$ $NAME$ {
    get {
      if (_$NAME$Offset == null) {
        _$NAME$Offset = Schema.GetOffset($HASH$);
      }
      return ref _Handle.$REF_METHOD$<$IMPL_TYPE$>(_$NAME$Offset!.Value);
    }
  }