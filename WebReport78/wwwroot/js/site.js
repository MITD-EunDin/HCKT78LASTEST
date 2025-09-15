// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// CLASS .CLEAR-ON-BLUR XÓA DỮ LIỆU Ô INPUT MỖI KHI THAO TÁC XONG
document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll(".clear-on-blur").forEach(function (el) {
        el.addEventListener("blur", function () {
            this.value = "";
        });
    });
});

// JS CỦA PROTECTDUTY -> INDEX
document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll('.toast').forEach(toastEl => {
        const toast = new bootstrap.Toast(toastEl, {
            delay: 2000, // 2 giây
            autohide: true
        });
        toast.show();
    });
    // JS để load modal Edit qua AJAX
    document.querySelectorAll('.edit-btn').forEach(button => {
        button.addEventListener('click', function () {
            const id = this.getAttribute('data-id');
            const url = '/ProtectDuty/Edit/' + id;

            fetch(url)
                .then(response => response.text())
                .then(html => {
                    // Insert HTML của partial view vào placeholder
                    document.getElementById('editModalPlaceholder').innerHTML = html;
                    // Show modal
                    const editModal = new bootstrap.Modal(document.getElementById('editModal'));
                    editModal.show();
                })
                .catch(error => console.error('Lỗi khi load modal Edit:', error));
        });
    });

    $('#searchDuty').on('click', function () {
        $('#loadingOverlay').show(); // Hiện overlay
    });

    // Khi trang load xong thì ẩn overlay
    $(window).on('load', function () {
        $('#loadingOverlay').hide();
    });
});

// NGĂN XÓA GIÁ TRỊ Ô FORMDATE VÀ TO DATE TRONG VIEW INOUT
document.addEventListener("DOMContentLoaded", function () {
    document.getElementById('toDate').addEventListener('keydown', function (e) {
        if (e.key === 'Backspace' || e.key === 'Delete') {
            if (e.target.value.length <= 1) {
                e.preventDefault(); // Ngăn hành động xóa
            }
        }
    });

    document.getElementById('toDate').addEventListener('input', function (e) {
        if (!e.target.value) {
            e.target.value = '@toDateTime'; // Khôi phục giá trị mặc định
        }
    });
});


// kích hoạt đóng modal export khi đã xuất excel thành công
document.addEventListener("DOMContentLoaded", function () {
    const form = document.getElementById("closeForm");
    form.addEventListener("submit", function () {
        // Lấy nút Hủy
        const closeBtn = document.getElementById("closeButton");
        if (closeBtn) {
            closeBtn.click(); // kích hoạt nút để đóng modal
        }
    });
});

// HIEEJN THI LOAS DU LIEU
$(document).ready(function () {
    // Khi form bấm "Load dữ liệu"
    $('#searchBtn').on('click', function () {
        $('#loadingOverlay').show(); // Hiện overlay
    });

    // Khi bấm vào phân trang
    $(document).on('click', '.page-link', function () {
        $('#loadingOverlay').show();
    });

    // Khi trang load xong thì ẩn overlay
    $(window).on('load', function () {
        $('#loadingOverlay').hide();
    });
});

// THEM XOA QUAN SO HIEN TAI
$(document).ready(function () {
    // Sử dụng event delegation để đảm bảo sự kiện hoạt động với modal động
    $(document).on('keypress', '#idCard', function (e) {
        if (e.which === 13) {
            e.preventDefault();
            // Mở modal nếu chưa mở
            if (!$('#addCurSoliderModal').is(':visible')) {
                $('#addCurSoliderModal').modal('show');
            }

            var idCard = $(this).val().trim();
            if (!idCard) {
                $('#formMessage').text('Vui lòng nhập mã định danh.');
                return;
            }

            console.log('Sending AJAX with idCard:', idCard);
            //console.log('Token:', $('input[name="__RequestVerificationToken"]').val());

            $.ajax({
                //url: '@Url.Action("CheckIdCard", "InOut")',
                url: checkIdCardUrl,
                type: 'POST',
                data: {
                    idCard: idCard,
                    __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val()
                },
                success: function (response) {
                    console.log('Response:', response);
                    if (response.success) {
                        $('#userGuid').val(response.data.userGuid_cur);
                        $('#name').val(response.data.name_cur);
                        $('#gender').val(response.data.gender_cur);
                        $('#phone').val(response.data.phoneNumber_cur);
                        $('#formMessage').text('').removeClass('text-danger').addClass('text-success');
                    } else {
                        $('#formMessage').text(response.message).removeClass('text-success').addClass('text-danger');
                        $('#userGuid').val('');
                        $('#name').val('');
                        $('#gender').val('');
                        $('#phone').val('');
                    }
                },
                error: function (xhr, status, error) {
                    console.log('AJAX Error:', error, xhr.responseText);
                    $('#formMessage').text('Đã xảy ra lỗi khi kiểm tra mã định danh: ' + error).removeClass('text-success').addClass('text-danger');
                    $('#userGuid').val('');
                    $('#name').val('');
                    $('#gender').val('');
                    $('#phone').val('');
                }
            });
        }
    });

    // Xử lý form submit để thêm quân nhân
    $('#addSoldierForm').on('submit', function (e) {
        e.preventDefault();
        var form = $(this);

        $.ajax({
            url: form.attr('action'),
            type: form.attr('method'),
            data: form.serialize(),
            success: function (response) {
                if (response.success) {
                    $('#formMessage').text(response.message).removeClass('text-danger').addClass('text-success');
                    form[0].reset();
                    location.reload();
                } else {
                    $('#formMessage').text(response.message).removeClass('text-success').addClass('text-danger');
                }
            },
            error: function (xhr, status, error) {
                console.log('AJAX Error:', error, xhr.responseText);
                $('#formMessage').text('Đã xảy ra lỗi khi thêm quân nhân: ' + error).removeClass('text-success').addClass('text-danger');
            }
        });
    });

    // Xử lý form xóa quân nhân
    $(document).on('submit', 'form[data-action="remove"]', function (e) {
        e.preventDefault();
        var form = $(this);
        var userGuid = form.find('input[name="userGuid"]').val();
        console.log('Sending remove request for userGuid:', userGuid);

        $.ajax({
            //url: '@Url.Action("RemoveCurrentSoldier", "InOut")',
            url: removeSoldierUrl,
            type: 'POST',
            data: {
                userGuid: userGuid,
                __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val()
            },
            success: function (response) {
                console.log('Remove response:', response);
                if (response.success) {
                    $('#tableMessage')
                        .text(response.message)
                        .removeClass('text-danger')
                        .addClass('text-success');
                    form.closest('tr').remove();
                    var soldierCurrent = parseInt($('#soldierCurrent').text()) - 1;
                    $('#soldierCurrent').text(soldierCurrent);
                } else {
                    $('#tableMessage')
                        .text(response.message)
                        .removeClass('text-success')
                        .addClass('text-danger');
                }
            },
            error: function (xhr, status, error) {
                console.log('Remove error:', error, xhr.responseText);
                $('#tableMessage')
                    .text('Đã xảy ra lỗi khi xóa quân nhân: ' + error)
                    .removeClass('text-success')
                    .addClass('text-danger');
            }
        });
    });

});