import sys

def main():
    try:
        with open('Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridge1504StaticAudit.cs', 'r') as f:
            lines = f.readlines()
            for i, line in enumerate(lines):
                if 'internal static void Run(string projectRoot)' in line:
                    start_line = i + 1
                if 'private static string ReadRequired(string projectRoot, string relativePath)' in line:
                    end_line = i
                    print(f"Run() method spans from line {start_line} to {end_line}")
                    print(f"Total lines: {end_line - start_line}")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == '__main__':
    main()
