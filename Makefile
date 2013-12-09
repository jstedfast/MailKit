OUTDIR=MailKit/bin/Release/lib/net45
ASSEMBLY=$(OUTDIR)/MailKit.dll
XMLDOCS=$(OUTDIR)/MailKit.xml

all:
	xbuild /target:Build /p:Configuration=Release MailKit.Net45.sln

debug:
	xbuild /target:Build /p:Configuration=Debug MailKit.Net45.sln

clean:
	xbuild /target:Clean /p:Configuration=Debug MailKit.Net45.sln
	xbuild /target:Clean /p:Configuration=Release MailKit.Net45.sln

update-docs: $(ASSEMBLY)
	mdoc update --delete -o docs/en $(ASSEMBLY)

merge-docs: $(ASSEMBLY) $(XMLDOCS)
	mdoc update -i $(XMLDOCS) -o docs/en $(ASSEMBLY)

assemble-docs:
	mdoc assemble --out=MailKit docs/en

html-docs:
	mdoc export-html --force-update --template=docs/github-pages.xslt -o ../MailKit-docs/docs docs/en
