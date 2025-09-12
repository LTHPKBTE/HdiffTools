Imports System.IO
Imports System.Text.Json
Imports ProtoBuf
Imports ZstdSharp

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

    Public Sub Ldiff转换(日志回调 As Action(Of String), ldiff路径 As String, hdiff输出路径 As String, 删除Ldiff As Boolean)
        Dim ldiff目录路径 As String = Path.Combine(ldiff路径, "ldiff")
        Dim 清单文件路径 As String = Path.Combine(ldiff路径, "manifest")

        日志回调("读取清单文件...")
        Dim 压缩数据 As Byte() = File.ReadAllBytes(清单文件路径)
        Dim 解压缩数据 As Byte()
        Using 解压缩器 As New Decompressor()
            解压缩数据 = 解压缩器.Unwrap(压缩数据).ToArray()
        End Using

        Dim 清单Proto As ManifestProto
        Using ms As New MemoryStream(解压缩数据)
            清单Proto = Serializer.Deserialize(Of ManifestProto)(ms)
        End Using

        Dim ldiff文件列表 As String() = Directory.GetFiles(ldiff目录路径)
        日志回调($"找到 {ldiff文件列表.Length} 个ldiff文件")

        日志回调("开始转换ldiff文件...")
        Dim 已处理数量 As Integer = 0
        For Each 当前Ldiff文件 In ldiff文件列表
            Dim asset名称 As String = Path.GetFileName(当前Ldiff文件)

            Dim 匹配的Assets列表 As New List(Of (AssetName As String, AssetSize As Long, Asset As AssetManifest))
            For Each assets组 In 清单Proto.Assets
                If assets组.AssetData IsNot Nothing Then
                    For Each assets In assets组.AssetData.Assets
                        If assets.ChunkFileName = asset名称 Then
                            匹配的Assets列表.Add((assets组.AssetName, assets组.AssetSize, assets))
                        End If
                    Next
                End If
            Next

            For Each 当前匹配 In 匹配的Assets列表
                Try
                    提取Ldiff文件(当前匹配.Asset, 当前匹配.AssetName, 当前匹配.AssetSize, ldiff目录路径, hdiff输出路径)
                    已处理数量 += 1
                Catch ex As Exception
                    日志回调($"处理 {当前匹配.AssetName} 时出错: {ex.Message}")
                End Try
            Next
        Next
        日志回调($"已转换 {已处理数量} 个文件")

        日志回调("生成 hdiffMap ...")
        Dim 差异映射名称列表 As New List(Of String)(ldiff文件列表.Length)
        For Each 当前条目 In ldiff文件列表
            差异映射名称列表.Add(Path.GetFileName(当前条目))
        Next

        Dim 映射 As HDiffMap = 生成HdiffMap(清单Proto, 差异映射名称列表)
        Dim 映射JSON As String = JsonSerializer.Serialize(映射, New JsonSerializerOptions With {.WriteIndented = True})
        Dim 映射JSON路径 As String = Path.Combine(hdiff输出路径, "hdiffmap.json")
        File.WriteAllText(映射JSON路径, 映射JSON)

        If 删除Ldiff Then
            Directory.Delete(ldiff路径, True)
        End If

        日志回调("Ldiff 转换已完成")
    End Sub
End Module
