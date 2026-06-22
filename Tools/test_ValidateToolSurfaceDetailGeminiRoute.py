import pytest
from pathlib import Path
from ValidateToolSurfaceDetailGeminiRoute import read_guid, ValidationError, display_path

def test_read_guid_success(tmp_path):
    asset_file = tmp_path / "test_asset.mat"
    meta_file = tmp_path / "test_asset.mat.meta"

    # Create fake meta file with YAML structure and GUID
    meta_content = """fileFormatVersion: 2
guid: 1234567890abcdef1234567890abcdef
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 2100000
  userData:
  assetBundleName:
  assetBundleVariant:
"""
    meta_file.write_text(meta_content, encoding="utf-8-sig")

    # Execute the function
    guid = read_guid(asset_file)

    # Assert correctness
    assert guid == "1234567890abcdef1234567890abcdef"

def test_read_guid_missing_meta_file(tmp_path):
    asset_file = tmp_path / "missing_meta.mat"
    # Ensure meta file does not exist
    meta_file = tmp_path / "missing_meta.mat.meta"
    if meta_file.exists():
        meta_file.unlink()

    with pytest.raises(ValidationError) as excinfo:
        read_guid(asset_file)

    assert "Missing meta file:" in str(excinfo.value)
    assert display_path(asset_file) in str(excinfo.value)

def test_read_guid_no_guid_in_meta(tmp_path):
    asset_file = tmp_path / "no_guid.mat"
    meta_file = tmp_path / "no_guid.mat.meta"

    # Create fake meta file without a GUID
    meta_content = """fileFormatVersion: 2
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 2100000
"""
    meta_file.write_text(meta_content, encoding="utf-8-sig")

    # Execute the function
    guid = read_guid(asset_file)

    # Assert correctness
    assert guid == ""
