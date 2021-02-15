package com.azure.tools.apiview.processor.model.maven;

import org.w3c.dom.Document;
import org.w3c.dom.Node;
import org.w3c.dom.NodeList;
import org.xml.sax.SAXException;

import javax.xml.parsers.DocumentBuilder;
import javax.xml.parsers.DocumentBuilderFactory;
import javax.xml.parsers.ParserConfigurationException;
import javax.xml.xpath.XPath;
import javax.xml.xpath.XPathConstants;
import javax.xml.xpath.XPathExpressionException;
import javax.xml.xpath.XPathFactory;
import java.io.IOException;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.List;

public class Pom implements MavenGAV {
    private Gav gav;
    private Gav parent;
    private List<Dependency> dependencies;

    private Float jacocoMinLineCoverage;
    private Float jacocoMinBranchCoverage;

    private String checkstyleExcludes;

    // These are the dependencies specifies in the maven-enforcer that are allowed
    private List<String> allowedDependencies;

    public Pom(final String groupId, final String artifactId, final String version) {
        this.gav = new Gav(groupId, artifactId, version);
    }

    public Pom(InputStream pomFileStream) throws IOException {
        this.dependencies = new ArrayList<>();
        this.allowedDependencies = new ArrayList<>();

        try {
            // use xpath to get the artifact ID
            final DocumentBuilderFactory builderFactory = DocumentBuilderFactory.newInstance();
            final DocumentBuilder builder = builderFactory.newDocumentBuilder();
            final Document xmlDocument = builder.parse(pomFileStream);
            final XPath xPath = XPathFactory.newInstance().newXPath();

            this.gav = createGav(xPath, xmlDocument, "/project");
            this.parent = createGav(xPath, xmlDocument, "/project/parent");

            // jacoco configuration
            Node n = (Node) xPath.evaluate("/project/properties/jacoco.min.linecoverage", xmlDocument, XPathConstants.NODE);
            this.jacocoMinLineCoverage = n == null ? null : Float.parseFloat(n.getTextContent());

            n = (Node) xPath.evaluate("/project/properties/jacoco.min.branchcoverage", xmlDocument, XPathConstants.NODE);
            this.jacocoMinBranchCoverage = n == null ? null : Float.parseFloat(n.getTextContent());

            // checkstyle excludes
            n = (Node) xPath.evaluate("/project/build/plugins/plugin/artifactId[text()='maven-checkstyle-plugin']/../configuration/excludes", xmlDocument, XPathConstants.NODE);
            this.checkstyleExcludes = n == null ? null : n.getTextContent();

            // actual dependencies
            final String dependencyExpression = "/project/dependencies/dependency";
            NodeList dependenciesNodeList = (NodeList) xPath.evaluate(dependencyExpression, xmlDocument, XPathConstants.NODESET);
            for (int i = 0; i < dependenciesNodeList.getLength(); i++) {
                Node dep = dependenciesNodeList.item(i);
                String depGroupId = xPath.evaluate("groupId", dep);
                String depArtifactId = xPath.evaluate("artifactId", dep);
                String depVersion = xPath.evaluate("version", dep);
                String depScope = xPath.evaluate("scope", dep);
                dependencies.add(new Dependency(depGroupId, depArtifactId, depVersion, depScope));
            }

            // allowed dependencies
            final String allowedDependencies = "/project/build/plugins/plugin/artifactId[text()='maven-enforcer-plugin']/../configuration/rules/bannedDependencies/includes/include";
            dependenciesNodeList = (NodeList) xPath.evaluate(allowedDependencies, xmlDocument, XPathConstants.NODESET);
            for (int i = 0; i < dependenciesNodeList.getLength(); i++) {
                this.allowedDependencies.add(dependenciesNodeList.item(i).getTextContent().trim());
            }
        } catch (ParserConfigurationException | SAXException | XPathExpressionException e) {
            e.printStackTrace();
        }
    }

    public String getGroupId() {
        return gav.getGroupId() != null ? gav.getGroupId() : parent.getGroupId();
    }

    public String getArtifactId() {
        return gav.getArtifactId();
    }

    public String getVersion() {
        return gav.getVersion() != null ? gav.getVersion() : parent.getVersion();
    }

    public Gav getParent() {
        return parent;
    }

    public List<Dependency> getDependencies() {
        return dependencies;
    }

    public Float getJacocoMinLineCoverage() {
        return jacocoMinLineCoverage;
    }

    public Float getJacocoMinBranchCoverage() {
        return jacocoMinBranchCoverage;
    }

    public String getCheckstyleExcludes() {
        return checkstyleExcludes;
    }

    public List<String> getAllowedDependencies() {
        return allowedDependencies;
    }

    private Gav createGav(final XPath xPath, final Document xmlDocument, final String root) throws XPathExpressionException {
        final String groupIdExpression = root + "/groupId";
        final Node groupIdNode = (Node) xPath.evaluate(groupIdExpression, xmlDocument, XPathConstants.NODE);
        final String groupId = groupIdNode.getTextContent();

        final String artifactIdExpression = root + "/artifactId";
        final Node artifactIdNode = (Node) xPath.evaluate(artifactIdExpression, xmlDocument, XPathConstants.NODE);
        final String artifactId = artifactIdNode.getTextContent();

        final String versionExpression = root + "/version";
        final Node versionNode = (Node) xPath.evaluate(versionExpression, xmlDocument, XPathConstants.NODE);
        final String version = versionNode.getTextContent();

        return new Gav(groupId, artifactId, version);
    }
}
