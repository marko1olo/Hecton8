1. **Refactor ShapesMath.cs:** Add `GetBezierPointCount` to centralize the bezier point count calculation.
2. **Refactor PolylinePath.cs:** Remove `CalcBezierPointCount` and use `ShapesMath.GetBezierPointCount`.
3. **Refactor PolygonPath.cs:**
    - Rewrite `BezierTo` and `ArcTo` to match the exact pattern and structure of the vector inputs in `PolylinePath.cs`.
    - Use `ShapesMath.GetBezierPointCount` to fix the bug where `pointsPerTurn` was ignored.
    - Make them inline expression-bodied methods `[MethodImpl( INLINE )]` to share the exact same style.
    - Remove the `Color` overload of `ArcTo` since `PolygonPath` does not support colors.
4. **Complete pre commit steps:** Complete pre commit steps to make sure proper testing, verifications, reviews and reflections are done.
