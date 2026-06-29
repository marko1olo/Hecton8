import sys

# Is caching the Transform enough of an optimization?
# `owner.transform` calls C++ getter `get_transform()` on `Component`.
# Inside the loop:
# `owner.transform.GetChild(i)` calls `get_transform()` and then `GetChild(i)`.
# By caching `Transform ownerTransform = owner.transform;`, we save N calls to `get_transform()`.
# This is a very classic loop-optimization! "Loop Invariant Code Motion".
# In Unity, `transform` is a property that calls into C++. Caching it outside a loop is the standard best practice for performance.
