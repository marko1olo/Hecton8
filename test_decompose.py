import re

def get_method():
    with open('Assets/_Project/Scripts/SaveBinaryStorage.cs', 'r') as f:
        content = f.read()

    start = content.find('private static bool TryWriteSaveFileIndexedV8')
    if start == -1:
        # Check for private static unsafe bool
        start = content.find('private static unsafe bool TryWriteSaveFileIndexedV8')
        if start == -1:
            return "Method not found"

    end = content.find('private static unsafe bool TryWriteSavePayloadMetadataV8', start)
    return content[start:end]

print(get_method())
