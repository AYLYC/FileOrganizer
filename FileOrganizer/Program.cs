using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography; // ハッシュ値計算のため
using System.Linq; // LINQを使用するためは厳密には不要ですが、一般的な慣習として置いています

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("重複ファイル削除ツールへようこそ！");

        // コマンドライン引数の解析
        if (args.Length == 1)
        {
            string targetFolderPath = args[0];
            Console.WriteLine($"\n指定されたフォルダ '{targetFolderPath}' の重複ファイルを検索・削除します。");
            FindAndDeleteDuplicateFiles(targetFolderPath);
        }
        else
        {
            Console.WriteLine("\n使用方法: FileOrganizer.exe <対象フォルダのパス>");
            Console.WriteLine("例: FileOrganizer.exe \"C:\\Users\\YourUser\\Downloads\"");
            Console.WriteLine("　　（パスにスペースが含まれる場合は、ダブルクォーテーションで囲んでください）");
        }

        Console.WriteLine("\nプログラムを終了します。何かキーを押してください...");
        Console.ReadKey();
    }

    /// <summary>
    /// 指定されたフォルダ内の重複ファイルを検索し、削除します。
    /// </summary>
    /// <param name="targetPath">重複ファイルを検索するフォルダのパス。</param>
    static void FindAndDeleteDuplicateFiles(string targetPath)
    {
        if (!Directory.Exists(targetPath))
        {
            Console.WriteLine("エラー: 指定されたフォルダが存在しません。パスを確認してください。");
            return;
        }

        Console.WriteLine($"\n'{targetPath}' 内のファイルをスキャンして重複を検索中...");

        // キーがハッシュ値、値がそのハッシュ値を持つファイルのパスのリスト
        Dictionary<string, List<string>> fileHashes = new Dictionary<string, List<string>>();
        int filesScanned = 0;

        // 指定されたフォルダとそのサブフォルダ内のすべてのファイルを取得
        // エラーハンドリングを強化し、アクセス拒否されたディレクトリはスキップ
        IEnumerable<string> allFiles;
        try
        {
            allFiles = Directory.EnumerateFiles(targetPath, "*.*", SearchOption.AllDirectories);
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"エラー: フォルダへのアクセスが拒否されました。管理者権限で実行するか、アクセス権を確認してください。詳細: '{ex.Message}'");
            return;
        }
        catch (DirectoryNotFoundException)
        {
            Console.WriteLine("エラー: 指定されたフォルダが見つかりません。");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"エラー: ファイルリストの取得中に予期せぬエラーが発生しました: {ex.Message}");
            return;
        }

        // ファイルをスキャンしてハッシュ値を計算
        foreach (string filePath in allFiles)
        {
            filesScanned++;
            // コンソールで進捗を更新表示
            Console.Write($"\rスキャン中: {filesScanned} 個のファイルを処理済み... 現在: {Path.GetFileName(filePath)}");

            // 隠しファイルやシステムファイルはデフォルトでスキップ
            FileAttributes attributes = File.GetAttributes(filePath);
            if ((attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
                (attributes & FileAttributes.System) == FileAttributes.System)
            {
                continue;
            }

            string fileHash = GetFileHash(filePath);

            if (fileHash == null)
            {
                // ハッシュ計算に失敗した場合はスキップ（エラーはGetFileHash内で表示済み）
                continue;
            }

            if (fileHashes.ContainsKey(fileHash))
            {
                fileHashes[fileHash].Add(filePath);
            }
            else
            {
                fileHashes.Add(fileHash, new List<string> { filePath });
            }
        }
        Console.WriteLine("\nスキャン完了。");

        // 重複ファイルグループを抽出
        List<List<string>> duplicateFileGroups = new List<List<string>>();
        foreach (var entry in fileHashes)
        {
            if (entry.Value.Count > 1) // 同じハッシュ値を持つファイルが複数ある場合、それは重複ファイルグループ
            {
                duplicateFileGroups.Add(entry.Value);
            }
        }

        if (duplicateFileGroups.Count == 0)
        {
            Console.WriteLine("\n重複ファイルは見つかりませんでした。");
            return;
        }

        Console.WriteLine($"\n--- {duplicateFileGroups.Count} 個の重複ファイルグループが見つかりました ---");
        int totalDuplicatesToDelete = 0;
        foreach (var group in duplicateFileGroups)
        {
            Console.WriteLine("\n--- 重複ファイルグループ ---");
            // 各グループの最初のファイルは「オリジナル」として扱い、それ以外を削除候補とする
            for (int i = 0; i < group.Count; i++)
            {
                Console.WriteLine($"  [{i + 1}] {group[i]} {(i == 0 ? "(オリジナルとして残す)" : "(削除候補)")}");
            }
            totalDuplicatesToDelete += (group.Count - 1);
        }

        Console.WriteLine($"\n合計で {totalDuplicatesToDelete} 個の重複ファイルが削除対象となります。");
        Console.Write("これらのファイルを削除しますか？ (y/n): ");
        string confirmation = Console.ReadLine();

        if (confirmation.ToLower() == "y")
        {
            int deletedCount = 0;
            foreach (var group in duplicateFileGroups)
            {
                // 最初のファイルを除き、他のファイルを削除
                for (int i = 1; i < group.Count; i++)
                {
                    try
                    {
                        File.Delete(group[i]);
                        Console.WriteLine($"削除しました: {group[i]}");
                        deletedCount++;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Console.WriteLine($"エラー: '{group[i]}' はアクセス拒否のため削除できませんでした。");
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"エラー: '{group[i]}' の削除中にI/Oエラーが発生しました: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"エラー: '{group[i]}' の削除中に予期せぬエラーが発生しました: {ex.Message}");
                    }
                }
            }
            Console.WriteLine($"\n{deletedCount} 個の重複ファイルを削除しました。");
        }
        else
        {
            Console.WriteLine("重複ファイルの削除はキャンセルされました。");
        }
    }

    /// <summary>
    /// 指定されたファイルのSHA256ハッシュ値を計算します。
    /// </summary>
    /// <param name="filePath">ハッシュ値を計算するファイルのパス。</param>
    /// <returns>ファイルのSHA256ハッシュ値の文字列、または計算失敗時はnull。</returns>
    static string GetFileHash(string filePath)
    {
        try
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"\n警告: ファイル '{filePath}' が見つかりません。スキップします。");
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine($"\n警告: ファイル '{filePath}' へのアクセスが拒否されました。スキップします。");
            return null;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"\n警告: ファイル '{filePath}' を読み取り中にI/Oエラーが発生しました: {ex.Message}。スキップします。");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nエラー: '{filePath}' のハッシュ計算中に予期せぬエラーが発生しました: {ex.Message}。スキップします。");
            return null;
        }
    }
}