window.deyasInstitutionalBackground = {
  refresh(version) {
    const url = `/institutional-background?v=${encodeURIComponent(version)}`;
    document.documentElement.style.setProperty('--institutional-background', `url("${url}")`);
  }
};
