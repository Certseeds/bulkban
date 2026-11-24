// 拖放支持:把 drop 进来的文件塞给 Blazor 的隐藏 <input type=file>。
// InputFile 自身不接收拖放,只能通过 DataTransfer 赋值。
window.bulkbanDrop = {
    attach: function (inputElementId, dropElementId) {
        const inputElement = document.getElementById(inputElementId);
        const dropElement = document.getElementById(dropElementId);
        if (!inputElement || !dropElement || dropElement.dataset.bound === '1') {
            return;
        }
        dropElement.dataset.bound = '1';

        ['dragenter', 'dragover'].forEach(function (name) {
            dropElement.addEventListener(name, function (e) {
                e.preventDefault();
                e.stopPropagation();
                dropElement.classList.add('hover');
            });
        });

        ['dragleave', 'dragend'].forEach(function (name) {
            dropElement.addEventListener(name, function (e) {
                e.preventDefault();
                e.stopPropagation();
                dropElement.classList.remove('hover');
            });
        });

        dropElement.addEventListener('drop', function (e) {
            e.preventDefault();
            e.stopPropagation();
            dropElement.classList.remove('hover');
            if (e.dataTransfer && e.dataTransfer.files.length > 0) {
                inputElement.files = e.dataTransfer.files;
                inputElement.dispatchEvent(new Event('change', { bubbles: true }));
            }
        });
    }
};

window.bulkbanClick = function (id) {
    const el = document.getElementById(id);
    if (el) { el.click(); }
};

// 下载:内容由 .NET 侧以 base64 传入,避免额外的 JS 互操作接口。
window.bulkbanDownload = function (fileName, base64) {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }
    const blob = new Blob([bytes], { type: 'application/zip' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
};
