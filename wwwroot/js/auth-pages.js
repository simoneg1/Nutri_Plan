document.addEventListener('DOMContentLoaded', function () {
    // Effetto di focus sui campi di input
    const formControls = document.querySelectorAll('.form-control');

    formControls.forEach(control => {
        control.addEventListener('focus', function () {
            this.parentElement.classList.add('input-focused');
        });

        control.addEventListener('blur', function () {
            if (this.value === '') {
                this.parentElement.classList.remove('input-focused');
            }
        });

        // Controlla se il campo ha già un valore (in caso di errore o autocompletamento)
        if (control.value !== '') {
            control.parentElement.classList.add('input-focused');
        }
    });

    // Aggiungi classe per mostrare le animazioni
    document.body.classList.add('ready');
});