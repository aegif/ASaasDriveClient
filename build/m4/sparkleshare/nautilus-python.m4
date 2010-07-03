AC_DEFUN([SPARKLESHARE_NAUTILUS_PYTHON],
[
	PKG_CHECK_MODULES(NAUTILUS_PYTHON, nautilus-python, have_nautilus_python=yes, have_nautilus_python=no)
	if test "x$have_nautilus_python" = "xyes"; then
		NAUTILUS_LIBDIR="`$PKG_CONFIG --variable=libdir nautilus-python`"
		AC_SUBST(NAUTILUS_LIBDIR)
		NAUTILUS_PYTHON_DIR="`$PKG_CONFIG --variable=pythondir nautilus-python`"
		AC_SUBST(NAUTILUS_PYTHON_DIR)
		AM_CONDITIONAL(NAUTILUS_EXTENSION_ENABLED, true)
	else
		AM_CONDITIONAL(NAUTILUS_EXTENSION_ENABLED, false)
	fi
])

