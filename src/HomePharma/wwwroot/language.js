window.medikitLanguage = (code, errorText) => {
    document.documentElement.lang = code;
    const error = document.getElementById('blazor-error-ui');
    if (error) error.textContent = errorText;
};
