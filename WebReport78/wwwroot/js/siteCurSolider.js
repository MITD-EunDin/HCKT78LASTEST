//document.getElementById('idCard')?.addEventListener('keypress', function (event) {
//    if (event.key === 'Enter') {
//        event.preventDefault();
//        var idCard = this.value;
//        if (idCard) {
//            fetch('/InOut/CheckIdCard', {
//                method: 'POST',
//                headers: {
//                    'Content-Type': 'application/json',
//                    'Accept': 'application/json'
//                },
//                body: JSON.stringify({ idCard: idCard })
//            })
//                .then(response => response.json())
//                .then(data => {
//                    var messageDiv = document.getElementById('formMessage');
//                    if (data.success) {
//                        document.getElementById('userGuid').value = data.data.UserGuid || '';
//                        document.getElementById('name').value = data.data.Name || '';
//                        document.getElementById('gender').value = data.data.Gender || '';
//                        document.getElementById('typePerson').value = data.data.TypePerson || '';
//                        document.getElementById('phone').value = data.data.Phone || '';
//                        messageDiv.innerHTML = '<div class="alert alert-success">Đã tìm thấy thông tin quân nhân.</div>';
//                    } else {
//                        document.getElementById('userGuid').value = '';
//                        document.getElementById('name').value = '';
//                        document.getElementById('gender').value = '';
//                        document.getElementById('typePerson').value = '';
//                        document.getElementById('phone').value = '';
//                        messageDiv.innerHTML = '<div class="alert alert-warning">' + data.message + '</div>';
//                    }
//                })
//                .catch(error => {
//                    document.getElementById('formMessage').innerHTML = '<div class="alert alert-danger">Lỗi: ' + error.message + '</div>';
//                });
//        }
//    }
//});

//document.getElementById('addSoldierForm')?.addEventListener('submit', function (event) {
//    event.preventDefault();
//    var form = this;
//    fetch(form.action, {
//        method: 'POST',
//        body: new FormData(form),
//        headers: {
//            'Accept': 'application/json'
//        }
//    })
//        .then(response => response.text())
//        .then(data => {
//            document.getElementById('formMessage').innerHTML = '<div class="alert alert-success">' + data + '</div>';
//            form.reset();
//            location.reload();
//        })
//        .catch(error => {
//            document.getElementById('formMessage').innerHTML = '<div class="alert alert-danger">Lỗi: ' + error.message + '</div>';
//        });
//});