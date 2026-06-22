import unittest
from OOP_VoxelPaging_Scanner import extract_method

class TestOOPVoxelPagingScanner(unittest.TestCase):
    def test_extract_method_happy_path(self):
        source = """
public void TestMethod() {
    int x = 5;
    return x;
}
        """
        signature = "public void TestMethod()"
        expected = """public void TestMethod() {
    int x = 5;
    return x;
}"""
        self.assertEqual(extract_method(source, signature), expected)

    def test_extract_method_nested_braces(self):
        source = """
public void TestMethod() {
    if (true) {
        int x = 5;
    }
    return;
}
        """
        signature = "public void TestMethod()"
        expected = """public void TestMethod() {
    if (true) {
        int x = 5;
    }
    return;
}"""
        self.assertEqual(extract_method(source, signature), expected)

    def test_extract_method_braces_on_next_line(self):
        source = """
public void TestMethod()
{
    int x = 5;
    return x;
}
        """
        signature = "public void TestMethod()"
        expected = """public void TestMethod()
{
    int x = 5;
    return x;
}"""
        self.assertEqual(extract_method(source, signature), expected)

    def test_extract_method_missing_signature(self):
        source = """
public void OtherMethod() {
    return;
}
        """
        signature = "public void TestMethod()"
        self.assertEqual(extract_method(source, signature), "")

    def test_extract_method_unbalanced_braces(self):
        source = """
public void TestMethod() {
    if (true) {
        return;
        """
        signature = "public void TestMethod()"
        self.assertEqual(extract_method(source, signature), "")

    def test_extract_method_no_braces(self):
        source = """
public void TestMethod();
        """
        signature = "public void TestMethod()"
        self.assertEqual(extract_method(source, signature), "")

    def test_extract_method_multiple_methods(self):
        source = """
public void OtherMethod() {
    return;
}

public void TestMethod() {
    int x = 5;
    return x;
}

public void AnotherMethod() {
    return;
}
        """
        signature = "public void TestMethod()"
        expected = """public void TestMethod() {
    int x = 5;
    return x;
}"""
        self.assertEqual(extract_method(source, signature), expected)

    def test_extract_method_same_line_body(self):
        source = """
public void TestMethod() { return; }
        """
        signature = "public void TestMethod()"
        expected = "public void TestMethod() { return; }"
        self.assertEqual(extract_method(source, signature), expected)

if __name__ == '__main__':
    unittest.main()
