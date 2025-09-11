Imports System.IO

Public Module Ldiff处理
    Public Function 生成HdiffMap(清单 As ManifestProto, chunk名称列表 As List(Of String)) As HDiffMap
        Dim hdiff映射 = New HDiffMap()
        hdiff映射.DiffMap = New List(Of HDiffData)

        For Each assetProto In 清单.Assets
            Dim assetName = assetProto.AssetName
            Dim assetSize = assetProto.AssetSize

            If assetProto.AssetData IsNot Nothing Then
                Dim chunk = assetProto.AssetData

                For Each assetData In chunk.Assets
                    Dim 是否找到区块名称 As Boolean = False
                    For Each chunk名称 In chunk名称列表
                        If chunk名称 = assetData.ChunkFileName Then
                            是否找到区块名称 = True
                            Exit For
                        End If
                    Next

                    Dim 是否需要差异 As Boolean = (assetData.OriginalFileSize <> 0) OrElse (assetData.HdiffFileSize <> assetSize)

                    If 是否找到区块名称 And 是否需要差异 Then
                        hdiff映射.DiffMap.Add(New HDiffData With {
                            .SourceFileName = assetData.OriginalFilePath,
                            .TargetFileName = assetName,
                            .PatchFileName = assetName & ".hdiff"
                        })
                    End If
                Next
            End If
        Next

        Return hdiff映射
    End Function

    Private Sub 复制文件段(输入路径 As String, 输出路径 As String, 偏移量 As Long, 大小 As Long)
        Using 输入流 = New FileStream(输入路径, FileMode.Open, FileAccess.Read, FileShare.Read)
            Using 输出流 = New FileStream(输出路径, FileMode.Create, FileAccess.Write)
                输入流.Seek(偏移量, SeekOrigin.Begin)

                Dim 缓冲区大小 As Integer = 81920
                Dim 缓冲区(缓冲区大小 - 1) As Byte
                Dim 已读取 As Integer
                Dim 剩余字节 As Long = 大小

                While 剩余字节 > 0
                    已读取 = 输入流.Read(缓冲区, 0, CInt(Math.Min(缓冲区大小, 剩余字节)))
                    输出流.Write(缓冲区, 0, 已读取)
                    剩余字节 -= 已读取
                End While
            End Using
        End Using
    End Sub

    Public Sub 提取Ldiff文件(数据 As AssetManifest, assetName As String, assetSize As Long, ldiff目录 As String, 输出目录 As String)
        Dim ldiff文件路径 = Path.Combine(ldiff目录, 数据.ChunkFileName)
        If Not File.Exists(ldiff文件路径) Then
            Throw New FileNotFoundException($"文件不存在: {数据.ChunkFileName}", ldiff文件路径)
        End If

        Dim 扩展名 As String
        If 数据.OriginalFileSize <> 0 OrElse assetSize <> 数据.HdiffFileSize Then
            扩展名 = ".hdiff"
        Else
            扩展名 = ""
        End If

        Dim 输出路径 = Path.Combine(输出目录, $"{assetName}{扩展名}")

        Directory.CreateDirectory(Path.GetDirectoryName(输出路径))
        复制文件段(ldiff文件路径, 输出路径, 数据.HdiffFileInChunkOffset, 数据.HdiffFileSize)
    End Sub
End Module
